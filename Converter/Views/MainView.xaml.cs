using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Converter.Views;

public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();
        if (Left == 0 && Top == 0)
        {
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }
        if (Settings.Default.Format == "cbz")
            chkCbz.IsChecked = true;
        else if (Settings.Default.Format == "cbr")
            chkCbr.IsChecked = true;
        else
            chkImages.IsChecked = true;

        chkFolderOpen.IsChecked = Settings.Default.FolderOpen;
    }

    private void CheckBoxClick(object sender, RoutedEventArgs e)
    {
        CheckBox checkBox = (CheckBox)sender;

        if (checkBox.Name == "chkCbz")
        {
            chkCbr.IsChecked = false;
            chkImages.IsChecked = false;
        }
        else if (checkBox.Name == "chkCbr")
        {
            chkCbz.IsChecked = false;
            chkImages.IsChecked = false;
        }
        else if (checkBox.Name == "chkImages")
        {
            chkCbz.IsChecked = false;
            chkCbr.IsChecked = false;
        }
        else if (checkBox.Name == "chkFolderOpen")
            Settings.Default.FolderOpen = (bool)chkFolderOpen.IsChecked!;


    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        try
        {
            DragMove();
        }
        catch (InvalidOperationException) { }
    }

    private void Minimize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else WindowState = WindowState.Maximized;
    }    
}
