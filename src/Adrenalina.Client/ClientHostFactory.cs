using Adrenalina.Application;
using Adrenalina.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;

namespace Adrenalina.Client;

public static class ClientHostFactory
{
    public static IHost BuildInteractiveHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        ConfigureSharedServices(builder.Services);
        builder.Services.AddSingleton<WindowsKioskManager>();
        builder.Services.AddHostedService<ClientSyncWorker>();
        builder.Services.AddSingleton<MainWindow>();
        return builder.Build();
    }

    public static IHost BuildServiceHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWindowsService(service =>
            {
                service.ServiceName = "Adrenalina Client Service";
            })
            .ConfigureServices((_, services) =>
            {
                ConfigureSharedServices(services);
                services.AddHostedService<ClientServiceWorker>();
            })
            .Build();
    }

    private static void ConfigureSharedServices(IServiceCollection services)
    {
        var options = ClientOptionsStore.LoadOrCreate();
        var runtimeRoot = ResolveClientRuntimeRoot();
        var machineDirectory = Path.Combine(runtimeRoot, Environment.MachineName);

        services.AddSingleton(options);
        services.AddSingleton(new LocalClientStoragePaths
        {
            StateFilePath = Path.Combine(machineDirectory, "client-state.json"),
            RequestQueueFilePath = Path.Combine(machineDirectory, "client-requests.json")
        });
        services.AddSingleton<IClientRuntimeStore, JsonClientRuntimeStore>();
        services.AddHttpClient(
            "adrenalina-server",
            client =>
            {
                client.BaseAddress = BuildServerBaseUri(options.ServerBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        services.AddSingleton<ClientServerGateway>();
    }

    private static Uri BuildServerBaseUri(string? serverBaseUrl)
    {
        if (Uri.TryCreate(serverBaseUrl?.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri;
        }

        return new Uri("http://127.0.0.1:5076/");
    }

    private static string ResolveClientRuntimeRoot()
    {
        var currentRoot = AdrenalinaPaths.GetClientRuntimeRoot();
        var currentMachineDirectory = Path.Combine(currentRoot, Environment.MachineName);
        if (Directory.Exists(currentMachineDirectory))
        {
            return currentRoot;
        }

        var legacyRoot = AdrenalinaPaths.GetLegacyClientRuntimeRoot();
        var legacyMachineDirectory = Path.Combine(legacyRoot, Environment.MachineName);
        if (!Directory.Exists(legacyMachineDirectory))
        {
            return currentRoot;
        }

        CopyDirectory(legacyMachineDirectory, currentMachineDirectory);
        return currentRoot;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(file));
            if (!File.Exists(destinationPath))
            {
                File.Copy(file, destinationPath);
            }
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(directory));
            CopyDirectory(directory, destinationPath);
        }
    }
}
