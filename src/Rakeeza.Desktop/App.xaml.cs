using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Rakeeza.Commands.Bridge;
using Rakeeza.Commands.System;

namespace Rakeeza.Desktop;

/// <summary>نقطة دخول المضيف: تبني حاوية الخدمات وتفتح النافذة الرئيسية.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddRakeezaBridge();
        services.AddSingleton<IHostEnvironmentInfo, DesktopEnvironmentInfo>();
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        MainWindow = new MainWindow(_services.GetRequiredService<CommandDispatcher>());
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
