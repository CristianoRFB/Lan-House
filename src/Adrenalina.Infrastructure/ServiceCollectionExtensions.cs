using Adrenalina.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Adrenalina.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdrenalinaServerPlatform(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var rootDirectory = configuration["Adrenalina:RootDirectory"];
        var baseDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? AdrenalinaPaths.GetAdminDataRoot()
            : rootDirectory;

        var paths = new AdrenalinaStoragePaths
        {
            DatabaseFilePath = Path.Combine(baseDirectory, "adrenalina.db"),
            BackupDirectory = Path.Combine(baseDirectory, "backups"),
            LogDirectory = Path.Combine(baseDirectory, "logs"),
            ClientRuntimeDirectory = AdrenalinaPaths.GetClientRuntimeRoot()
        };

        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(paths.BackupDirectory);
        Directory.CreateDirectory(paths.LogDirectory);

        services.AddSingleton(paths);
        services.AddDbContext<AdrenalinaDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabaseFilePath}"));

        services.AddScoped<CafeManagementService>();
        services.AddScoped<ICafeManagementService>(provider => provider.GetRequiredService<CafeManagementService>());
        services.AddScoped<IAdminAuthService>(provider => provider.GetRequiredService<CafeManagementService>());
        services.AddHostedService<ServerMaintenanceHostedService>();

        return services;
    }

    public static IServiceCollection AddAdrenalinaClientRuntimeStore(this IServiceCollection services, IConfiguration configuration)
    {
        var runtimeDirectory = configuration["Adrenalina:ClientRuntimeDirectory"];
        var baseDirectory = string.IsNullOrWhiteSpace(runtimeDirectory)
            ? AdrenalinaPaths.GetClientRuntimeRoot()
            : runtimeDirectory;

        var machineDirectory = Path.Combine(baseDirectory, Environment.MachineName);
        var paths = new LocalClientStoragePaths
        {
            StateFilePath = Path.Combine(machineDirectory, "client-state.json"),
            RequestQueueFilePath = Path.Combine(machineDirectory, "client-requests.json")
        };

        services.AddSingleton(paths);
        services.AddSingleton<IClientRuntimeStore, JsonClientRuntimeStore>();
        return services;
    }
}
