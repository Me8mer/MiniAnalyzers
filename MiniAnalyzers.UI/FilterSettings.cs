using System.ComponentModel;

namespace MiniAnalyzers.UI;

/// <summary>
/// User-editable filters applied to the diagnostics grid.
/// </summary>
public sealed class FilterSettings : INotifyPropertyChanged
{
    private bool showInfo = true;
    private bool showWarning = true;
    private bool showError = true;
    private string includeIdsCsv = string.Empty; // e.g. "MNA0001,MNA0004"
    private string excludeIdsCsv = string.Empty;

    private string searchText = string.Empty;    // matches Message, Analyzer, FilePath, ProjectName

    public bool ShowInfo
    {
        get => showInfo;
        set { if (showInfo != value) { showInfo = value; OnPropertyChanged(nameof(ShowInfo)); } }
    }

    public bool ShowWarning
    {
        get => showWarning;
        set { if (showWarning != value) { showWarning = value; OnPropertyChanged(nameof(ShowWarning)); } }
    }

    public bool ShowError
    {
        get => showError;
        set { if (showError != value) { showError = value; OnPropertyChanged(nameof(ShowError)); } }
    }

    public string IncludeIdsCsv
    {
        get => includeIdsCsv;
        set { if (includeIdsCsv != value) { includeIdsCsv = value; OnPropertyChanged(nameof(IncludeIdsCsv)); } }
    }


    public string ExcludeIdsCsv
    {
        get => excludeIdsCsv;
        set { if (excludeIdsCsv != value) { excludeIdsCsv = value; OnPropertyChanged(nameof(ExcludeIdsCsv)); } }
    }

    public string SearchText
    {
        get => searchText;
        set { if (searchText != value) { searchText = value; searchText = value; OnPropertyChanged(nameof(SearchText)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));







}




