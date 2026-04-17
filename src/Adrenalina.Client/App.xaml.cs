using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Adrenalina.Client;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _singleInstance;
    private IHost? _interactiveHost;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--watchdog", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            await ClientWatchdogRunner.RunAsync(e.Args);
            Shutdown();
            return;
        }

        if (e.Args.Contains("--service", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            using var serviceHost = ClientHostFactory.BuildServiceHost(e.Args);
            await serviceHost.RunAsync();
            Shutdown();
            return;
        }

        _singleInstance = SingleInstanceGuard.TryAcquire("Global\\Adrenalina.Client.UI");
        if (_singleInstance is null)
        {
            Shutdown();
            return;
        }

        _interactiveHost = ClientHostFactory.BuildInteractiveHost(e.Args);
        await _interactiveHost.StartAsync();

        var mainWindow = _interactiveHost.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        if (_interactiveHost is not null)
        {
            await _interactiveHost.StopAsync();
            _interactiveHost.Dispose();
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
