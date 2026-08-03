using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Adrenalina.Application;
using Adrenalina.Infrastructure;
using System.IO;
using System.Windows;

namespace Adrenalina.Client;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _singleInstance;
    private IHost? _interactiveHost;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, eventArgs) =>
        {
            var logPath = Path.Combine(AdrenalinaPaths.GetClientSettingsRoot(), "logs", "Client.log");
            AdrenalinaFileLog.Write(logPath, LogLevel.Error, "UI", "Erro não tratado na interface do cliente.", eventArgs.Exception);
            MessageBox.Show(
                "Ocorreu um erro inesperado. O cliente continuará tentando se recuperar.",
                "Adrenalina Client",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            eventArgs.Handled = true;
        };

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
