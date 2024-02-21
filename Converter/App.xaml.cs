using Converter.Themes;
using Converter.ViewModels;
using Converter.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using System.Windows.Markup;

namespace Converter;

public partial class App : Application
{
    public static IHost? AppHost { get; set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder().ConfigureServices((hostContext, services) =>
        {
            services.AddSingleton(typeof(MainView));
            services.AddSingleton(typeof(MainViewModel));
        }).Build();

        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("fr-BE")));
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost!.StartAsync();
        ThemeManager.LoadTheme(Settings.Default.Theme);

        MainView mainView = AppHost.Services.GetRequiredService<MainView>();
        MainViewModel mainViewModel = AppHost.Services.GetRequiredService<MainViewModel>();
        mainView.DataContext = mainViewModel;
        mainView.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        base.OnExit(e);
    }
}
