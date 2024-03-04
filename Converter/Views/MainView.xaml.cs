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

        chkInPdf.IsChecked = Settings.Default.InputFormat == "pdf";
        chkInCbz.IsChecked = Settings.Default.InputFormat == "cbz";
        chkInCbr.IsChecked = Settings.Default.InputFormat == "cbr";
        chkInImages.IsChecked = Settings.Default.InputFormat == "images";
        chkOutPdf.IsChecked = Settings.Default.OutputFormat == "pdf";
        chkOutCbz.IsChecked = Settings.Default.OutputFormat == "cbz";
        chkOutCbr.IsChecked = Settings.Default.OutputFormat == "cbr";
        chkOutImages.IsChecked = Settings.Default.OutputFormat == "images";
        chkFolderOpen.IsChecked = Settings.Default.FolderOpen == true;
    }

    void CheckBox(object sender, RoutedEventArgs e)
    {
        CheckBox checkBox = (CheckBox)sender;

        if (checkBox.Name == "chkFolderOpen")
            Settings.Default.FolderOpen = (bool)chkFolderOpen.IsChecked!;
        else if (checkBox.Name.Contains("In"))
        {
            chkInPdf.IsChecked = checkBox.Name == "chkInPdf";
            chkInCbz.IsChecked = checkBox.Name == "chkInCbz";
            chkInCbr.IsChecked = checkBox.Name == "chkInCbr";
            chkInImages.IsChecked = checkBox.Name == "chkInImages";
            Settings.Default.InputFormat = checkBox.Name.ToLower()[5..];            
        }
        else
        {
            chkOutPdf.IsChecked = checkBox.Name == "chkOutPdf";
            chkOutCbz.IsChecked = checkBox.Name == "chkOutCbz";
            chkOutCbr.IsChecked = checkBox.Name == "chkOutCbr";
            chkOutImages.IsChecked = checkBox.Name == "chkOutImages";
            Settings.Default.OutputFormat = checkBox.Name.ToLower()[6..];
        }
    }

    void UncheckBox(object sender, RoutedEventArgs e)
    {
        CheckBox checkBox = (CheckBox)sender;

        if (checkBox.Name.Contains("In"))
        {
            if (!(bool)chkInPdf.IsChecked! && !(bool)chkInCbz.IsChecked! && !(bool)chkInCbr.IsChecked! && !(bool)chkInImages.IsChecked!)
                checkBox.IsChecked = true;
        }
        else if (checkBox.Name.Contains("Out"))
        {
            if (!(bool)chkOutPdf.IsChecked! && !(bool)chkOutCbz.IsChecked! && !(bool)chkOutCbr.IsChecked! && !(bool)chkOutImages.IsChecked!)
                checkBox.IsChecked = true;
        }
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

    void Minimize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    void Maximize(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else WindowState = WindowState.Maximized;
    }   
}
