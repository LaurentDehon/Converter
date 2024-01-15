using Converter.Business;
using Converter.ViewModels;
using Converter.Themes;
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
            services.AddSingleton(typeof(ConverterService));
        }).Build();

        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("fr-BE")));
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost!.StartAsync();
        ThemeManager.LoadTheme(Settings.Default.Theme);

        MainView startupView = AppHost.Services.GetRequiredService<MainView>();
        startupView.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        base.OnExit(e);
    }
}
