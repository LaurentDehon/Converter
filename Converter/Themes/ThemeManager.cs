using System.Windows;

namespace Converter.Themes;

public static class ThemeManager
{
    public static void LoadTheme(string theme)
    {
        foreach (ResourceDictionary dictionnary in Application.Current.Resources.MergedDictionaries)
        {
            if (dictionnary.Source.OriginalString.ToString().Contains("Theme"))
            {
                Application.Current.Resources.MergedDictionaries.Remove(dictionnary);
                break;
            }
        }

        ResourceDictionary newTheme;
        try
        {
            newTheme = new() { Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative) };
        }
        catch (Exception)
        {
            newTheme = new() { Source = new Uri($"Themes/Dracula.xaml", UriKind.Relative) };
        }
        Application.Current.Resources.MergedDictionaries.Add(newTheme);
    }
}
