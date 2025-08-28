using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MiniAnalyzers.UI;

public partial class SettingsWindow : Window
{
    public SettingsWindow(FilterSettings settings, IEnumerable<RuleSummary> rules)
    {
        InitializeComponent();
        DataContext = settings; // Two-way binding directly edits the shared FilterSettings
        lstRules.ItemsSource = rules;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    private void AddInclude_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FilterSettings s) return;
        if (sender is Button b && b.Tag is string id) s.IncludeIdsCsv = AppendCsvUnique(s.IncludeIdsCsv, id);
    }

    private void AddExclude_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FilterSettings s) return;
        if (sender is Button b && b.Tag is string id) s.ExcludeIdsCsv = AppendCsvUnique(s.ExcludeIdsCsv, id);
    }

    private static string AppendCsvUnique(string csv, string id)
    {
        var set = csv.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0)
                     .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (set.Add(id))
            return string.Join(",", set);
        return csv;
    }
}