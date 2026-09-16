using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Agent;

internal sealed class AgentWorker(
    WindowsPolicyEnforcer enforcer,
    ILogger<AgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ipc = new AgentIpcServer(enforcer, logger);
        var ipcTask = ipc.RunAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (AgentSecretStore.Load() is null)
                {
                    logger.LogWarning("Agent sem segredo provisionado; a estação permanecerá bloqueada.");
                }
                else if (!enforcer.SessionActive && !enforcer.InMaintenance)
                {
                    enforcer.ApplyBlocked();
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha no ciclo de proteção da estação.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        await ipcTask;
    }
}
