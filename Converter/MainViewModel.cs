using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Converter.Business;
using Converter.Themes;
using System.Windows;

namespace Converter.ViewModels;

public partial class MainViewModel(ConverterService converterService) : ObservableObject
{
    readonly ConverterService converterService = converterService;

    [ObservableProperty] string? _theme;

    [RelayCommand]
    void ThemeSelect(string header)
    {
        ThemeManager.LoadTheme(header);
        Theme = $"Theme {header}";
        Settings.Default.Theme = header;
    }

    [RelayCommand]
    static void Close()
    {
        Settings.Default.Save();
        Application.Current.Shutdown();
    }
}
