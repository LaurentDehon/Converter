using System.Windows;
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
