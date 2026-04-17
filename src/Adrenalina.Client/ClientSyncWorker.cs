using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Client;

public sealed class ClientSyncWorker(
    ClientConnectionOptions options,
    ClientServerGateway gateway,
    ILogger<ClientSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sincronização do cliente iniciada para {MachineName}.", options.MachineName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await gateway.SyncOnceAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Falha ao sincronizar cliente.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(3, options.SyncIntervalSeconds)), stoppingToken);
        }
    }
}
