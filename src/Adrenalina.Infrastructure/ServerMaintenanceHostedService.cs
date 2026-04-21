using Adrenalina.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Adrenalina.Infrastructure;

public sealed class ServerMaintenanceHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ServerMaintenanceHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScopedAsync(service => service.RunMaintenanceTickAsync(stoppingToken), stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha na manutenção periódica do servidor.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunScopedAsync(service => service.RunMaintenanceTickAsync(cancellationToken), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao executar manutenção final no desligamento.");
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task RunScopedAsync(Func<ICafeManagementService, Task> operation, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICafeManagementService>();
        await operation(service);
    }
}
