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
        var runtimeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Adrenalina",
            "runtime");
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
                client.BaseAddress = new Uri(options.ServerBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        services.AddSingleton<ClientServerGateway>();
    }
}
