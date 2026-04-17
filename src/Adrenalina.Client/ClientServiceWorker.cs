using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Client;

public sealed class ClientServiceWorker(
    ClientConnectionOptions options,
    ILogger<ClientServiceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Serviço watchdog do cliente iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!IsInteractiveClientRunning())
                {
                    await TryStartInteractiveClientAsync(stoppingToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha no watchdog do serviço do cliente.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private static bool IsInteractiveClientRunning()
    {
        var currentProcess = Process.GetCurrentProcess();
        return Process.GetProcessesByName(currentProcess.ProcessName)
            .Any(process => process.Id != currentProcess.Id && process.SessionId > 0);
    }

    private async Task TryStartInteractiveClientAsync(CancellationToken cancellationToken)
    {
        // Segurança: serviços não conseguem exibir UI diretamente em sessão interativa no Windows 10.
        // O caminho suportado aqui é iniciar a tarefa agendada criada na instalação.
        if (!string.IsNullOrWhiteSpace(options.UiScheduledTaskName))
        {
            var startInfo = new ProcessStartInfo("schtasks.exe", $"/Run /TN \"{options.UiScheduledTaskName}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }
    }
}
