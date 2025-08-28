using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Win32;
using MiniAnalyzers.Core;
using MiniAnalyzers.Roslyn.Analyzers;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Data;

namespace MiniAnalyzers.UI;

public partial class MainWindow : Window
{
    // Bindable collection shown by the DataGrid.
    public ObservableCollection<DiagnosticInfo> Results { get; } = new();

    private CancellationTokenSource? _cts;
    public FilterSettings Filters { get; } = new();
    private ICollectionView? _resultsView;
    private List<RuleSummary> _knownRules = new();


    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _resultsView = CollectionViewSource.GetDefaultView(Results);
        _knownRules = GetActiveAnalyzers()
            .SelectMany(a => a.SupportedDiagnostics)
            .Select(d => new RuleSummary(d.Id, d.Title.ToString()))
            .GroupBy(r => r.Id).Select(g => g.First())  // avoid duplicates if any
            .OrderBy(r => r.Id)
            .ToList();
        _resultsView.Filter = FilterPredicate;

        // Any change to Filters triggers a view refresh.
        Filters.PropertyChanged += (_, __) => _resultsView?.Refresh();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Solutions or Projects|*.sln;*.csproj|Solution (*.sln)|*.sln|Project (*.csproj)|*.csproj",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
        {
            txtPath.Text = dlg.FileName;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    private void grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem != null)
        {
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(grid.SelectedItem);
            row.DetailsVisibility =
                row.DetailsVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Signal cancellation. Analyze_Click already handles TaskCanceledException.
        _cts?.Cancel();
        txtStatus.Text = "Cancel requested…";
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(Filters, _knownRules) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            // The dialog edits the shared Filters instance. Just refresh the view.
            _resultsView?.Refresh();
            txtStatus.Text = $"Diagnostics: {Results.Count} (showing {Results.Count(r => FilterPredicate(r))})";
        }
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        var path = txtPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Select a valid .sln or .csproj first.", "Mini Analyzers");
            return;
        }

        // Keep the first pass simple: disable UI and run.
        SetUiBusy(true);
        _cts = new CancellationTokenSource();
        Results.Clear();
        txtStatus.Text = "Analyzing…";

        try
        {
            // Choose analyzers you want to run from your library.
            var analyzers = GetActiveAnalyzers();

            var isSolution = path.EndsWith(".sln", System.StringComparison.OrdinalIgnoreCase);
            var list = isSolution
                ? await AnalysisRunner.AnalyzeSolutionAsync(path, analyzers, _cts.Token)
                : await AnalysisRunner.AnalyzeProjectAsync(path, analyzers, _cts.Token);

            _resultsView?.Refresh();
            foreach (var d in list.OrderBy(d => d.FilePath).ThenBy(d => d.Line).ThenBy(d => d.Column))
                Results.Add(d);

            txtStatus.Text = $"Diagnostics: {Results.Count} (showing {Results.Count(r => FilterPredicate(r))})";
        }
        catch (TaskCanceledException)
        {
            txtStatus.Text = "Canceled.";
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Analysis failed");
            txtStatus.Text = "Error.";
        }
        finally
        {
            SetUiBusy(false);
            _cts = null;
        }
    }

    private void SetUiBusy(bool busy)
    {
        btnAnalyze.IsEnabled = !busy;
        btnBrowse.IsEnabled = !busy;
        btnCancel.IsEnabled = busy;
    }

    private bool FilterPredicate(object item)
    {
        if (item is not DiagnosticInfo diagnostic)
            return false;

        // 1) Severity gate
        if (!IsSeverityAllowed(diagnostic.Severity))
            return false;

        // 2) ID include list (CSV). Empty means no restriction.
        if (!IsIdAllowed(diagnostic.Id))
            return false;

        // 3) Free-text search across several fields. Empty means no restriction.
        if (!MatchesSearch(diagnostic))
            return false;

        return true;
    }

    private bool IsSeverityAllowed(string severity)
    {
        // Keep the map simple and case-insensitive.
        var s = severity.Trim();
        if (s.Equals("Info", System.StringComparison.OrdinalIgnoreCase)) return Filters.ShowInfo;
        if (s.Equals("Warning", System.StringComparison.OrdinalIgnoreCase)) return Filters.ShowWarning;
        if (s.Equals("Error", System.StringComparison.OrdinalIgnoreCase)) return Filters.ShowError;
        // If something unexpected comes in, show it by default to avoid hiding data silently.
        return true;
    }

    private bool IsIdAllowed(string id)
    {
        // Build include and exclude sets on every call for simplicity.
        // If this shows up hot, we can cache and refresh on Filters change.
        var includeSet = SplitCsv(Filters.IncludeIdsCsv);
        var excludeSet = SplitCsv(Filters.ExcludeIdsCsv);

        if (excludeSet.Contains(id))
            return false;

        if (includeSet.Count == 0)
            return true; // no include restriction

        return includeSet.Contains(id);
    }
    private static HashSet<string> SplitCsv(string csv) =>
    csv.Split(',')
       .Select(p => p.Trim())
       .Where(p => p.Length > 0)
       .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private bool MatchesSearch(DiagnosticInfo d)
    {
        var q = Filters.SearchText;
        if (string.IsNullOrWhiteSpace(q))
            return true;

        // Case-insensitive contains across several useful fields.
        return Contains(d.Message, q)
            || Contains(d.Analyzer, q)
            || Contains(d.FilePath, q)
            || Contains(d.ProjectName, q)
            || Contains(d.Id, q);
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack?.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

    private static DiagnosticAnalyzer[] GetActiveAnalyzers() => new DiagnosticAnalyzer[]
    {
        new AsyncVoidAnalyzer(),        // MNA0001 Avoid 'async void' methods
        new ConsoleWriteLineAnalyzer(), // MNA0002 
        new EmptyCatchBlockAnalyzer(),  // MNA0002 Do not use empty 'catch' blocks
        new WeakVariableNameAnalyzer()  // MNA0004 Use descriptive names
    };


}
