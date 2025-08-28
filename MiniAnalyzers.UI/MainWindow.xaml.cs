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

namespace MiniAnalyzers.UI;

public partial class MainWindow : Window
{
    // Bindable collection shown by the DataGrid.
    public ObservableCollection<DiagnosticInfo> Results { get; } = new();

    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
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
            var analyzers = new DiagnosticAnalyzer[]
            {
                new AsyncVoidAnalyzer(),
                new EmptyCatchBlockAnalyzer(),
                new WeakVariableNameAnalyzer()
                // Add others here as you add them
            };

            var isSolution = path.EndsWith(".sln", System.StringComparison.OrdinalIgnoreCase);
            var list = isSolution
                ? await AnalysisRunner.AnalyzeSolutionAsync(path, analyzers, _cts.Token)
                : await AnalysisRunner.AnalyzeProjectAsync(path, analyzers, _cts.Token);

            foreach (var d in list.OrderBy(d => d.FilePath).ThenBy(d => d.Line).ThenBy(d => d.Column))
                Results.Add(d);

            txtStatus.Text = $"Diagnostics: {Results.Count}";
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
}
