using System;
using System.Linq;
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
        if (DataContext is not FilterSettings setting) return;
        if (sender is Button button && button.Tag is string id) setting.IncludeIdsCsv = AppendCsvUnique(setting.IncludeIdsCsv, id);
    }

    private void AddExclude_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FilterSettings setting) return;
        if (sender is Button button && button.Tag is string id) setting.ExcludeIdsCsv = AppendCsvUnique(setting.ExcludeIdsCsv, id);
    }

    private static string AppendCsvUnique(string csv, string id)
    {
        var exists = csv.Split(',')
                    .Select(t => t.Trim())
                    .Any(t => t.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (exists) return csv;
        return string.IsNullOrWhiteSpace(csv) ? id : $"{csv},{id}";
    }
}