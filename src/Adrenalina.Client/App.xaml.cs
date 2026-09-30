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
<<<<<<< HEAD
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
=======

        try
        {
            await StartCoreAsync(e.Args);
        }
        catch (Exception exception)
        {
            if (e.Args.Contains("--watchdog", StringComparer.OrdinalIgnoreCase) ||
                e.Args.Contains("--service", StringComparer.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine($"Falha no processo auxiliar do CLIENTE: {exception}");
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"Nao foi possivel iniciar o CLIENTE.\n\n{exception.Message}",
                    "Adrenalina CLIENTE",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            Shutdown();
        }
    }

    private async Task StartCoreAsync(string[] args)
    {
        if (args.Contains("--watchdog", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            await ClientWatchdogRunner.RunAsync(args);
            Shutdown();
            return;
        }

        if (args.Contains("--service", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            using var serviceHost = ClientHostFactory.BuildServiceHost(args);
            await serviceHost.RunAsync();
            Shutdown();
            return;
        }
>>>>>>> 9be62fb (Fixes)

        _singleInstance = SingleInstanceGuard.TryAcquire("Global\\Adrenalina.Client.UI");
        if (_singleInstance is null)
        {
            Shutdown();
            return;
        }

<<<<<<< HEAD
        await DiscoverServerAsync();

        _interactiveHost = ClientHostFactory.BuildInteractiveHost(e.Args);
=======
        _interactiveHost = ClientHostFactory.BuildInteractiveHost(args);
>>>>>>> 9be62fb (Fixes)
        await _interactiveHost.StartAsync();

        var mainWindow = _interactiveHost.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

<<<<<<< HEAD
    private static async Task DiscoverServerAsync()
    {
        var options = ClientOptionsStore.LoadOrCreate();
        if (options.SetupCompleted && !string.IsNullOrWhiteSpace(options.ServerBaseUrl))
        {
            return;
        }

        try
        {
            var discovered = await LanDiscoveryProtocol.DiscoverServerAsync(TimeSpan.FromSeconds(2));
            if (discovered is null)
            {
                return;
            }

            options.ServerBaseUrl = discovered.ToString();
            ClientOptionsStore.Save(options);
        }
        catch
        {
            // The setup screen remains available when discovery is unavailable.
        }
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
=======
    protected override void OnExit(System.Windows.ExitEventArgs e)
>>>>>>> 9be62fb (Fixes)
    {
        try
        {
            if (_interactiveHost is not null)
            {
                try
                {
                    Task.Run(async () => await _interactiveHost.StopAsync()).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"Falha ao encerrar CLIENTE: {exception}");
                }
                finally
                {
                    _interactiveHost.Dispose();
                }
            }
        }
        finally
        {
            _singleInstance?.Dispose();
            base.OnExit(e);
        }
    }
}
