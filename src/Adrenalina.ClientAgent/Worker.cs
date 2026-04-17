using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Adrenalina.Application;
using Adrenalina.Domain;
using Microsoft.Extensions.Options;

namespace Adrenalina.ClientAgent;

public sealed class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IClientRuntimeStore runtimeStore,
    IOptions<ClientAgentOptions> optionsAccessor) : BackgroundService
{
    private readonly ClientAgentOptions _options = optionsAccessor.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agente cliente iniciado para {MachineName}.", _options.MachineName);
        await runtimeStore.LoadStateAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncOnceAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(3, _options.SyncIntervalSeconds)), stoppingToken);
        }
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("adrenalina-server");
        var queuedRequests = await runtimeStore.DrainRequestsAsync(cancellationToken);

        try
        {
            if (queuedRequests.Count > 0)
            {
                var requestBatch = new ClientRequestBatchRequest
                {
                    MachineKey = _options.MachineKey,
                    Requests = queuedRequests
                };

                var requestResponse = await client.PostAsJsonAsync("api/client/requests", requestBatch, JsonDefaults.Options, cancellationToken);
                requestResponse.EnsureSuccessStatusCode();
            }

            var heartbeat = new ClientHeartbeatRequest
            {
                MachineKey = _options.MachineKey,
                MachineName = _options.MachineName,
                Hostname = Environment.MachineName,
                IpAddress = ResolveLocalIpAddress(),
                Kind = _options.MachineKind,
                Status = await ResolveMachineStatusAsync(cancellationToken),
                Processes = CollectProcesses()
            };

            var response = await client.PostAsJsonAsync("api/client/heartbeat", heartbeat, JsonDefaults.Options, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ClientHeartbeatResponse>(JsonDefaults.Options, cancellationToken)
                ?? new ClientHeartbeatResponse();

            var updatedState = payload.RuntimeState;
            updatedState = await ApplyCommandsAsync(updatedState, payload.Commands, cancellationToken);
            updatedState = AppendNotifications(updatedState, payload.Notifications);
            await runtimeStore.SaveStateAsync(updatedState, cancellationToken);
            await EnforceBlockedProgramsAsync(payload.Settings.BlockedProgramsCsv);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Servidor indisponível. Mantendo modo offline.");
            foreach (var item in queuedRequests)
            {
                await runtimeStore.EnqueueRequestAsync(item, cancellationToken);
            }

            var current = await runtimeStore.LoadStateAsync(cancellationToken);
            await runtimeStore.SaveStateAsync(CloneState(current, sessionMessage: "Servidor offline. O cliente segue operando e tentará sincronizar novamente."), cancellationToken);
        }
    }

    private async Task<MachineStatus> ResolveMachineStatusAsync(CancellationToken cancellationToken)
    {
        var state = await runtimeStore.LoadStateAsync(cancellationToken);
        if (state.IsLocked)
        {
            return MachineStatus.Locked;
        }

        return string.IsNullOrWhiteSpace(state.CurrentUserName) || state.CurrentUserName == "Aguardando login"
            ? MachineStatus.Idle
            : MachineStatus.InSession;
    }

    private static IReadOnlyList<ProcessDto> CollectProcesses()
    {
        try
        {
            return Process.GetProcesses()
                .OrderByDescending(process => process.WorkingSet64)
                .Take(12)
                .Select(process =>
                {
                    string title;
                    try
                    {
                        title = process.MainWindowTitle;
                    }
                    catch
                    {
                        title = string.Empty;
                    }

                    return new ProcessDto
                    {
                        ProcessName = $"{process.ProcessName}.exe",
                        WindowTitle = title,
                        MemoryMb = Math.Round(process.WorkingSet64 / 1024d / 1024d, 2)
                    };
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<ClientRuntimeState> ApplyCommandsAsync(
        ClientRuntimeState state,
        IReadOnlyList<RemoteCommandEnvelope> commands,
        CancellationToken cancellationToken)
    {
        var working = state;
        foreach (var command in commands)
        {
            switch (command.Type)
            {
                case RemoteCommandType.LockScreen:
                    working = CloneState(working, isLocked: true, sessionMessage: command.Message, lockMessage: command.Message);
                    break;
                case RemoteCommandType.ToggleTimerVisibility:
                    var show = ParseShowFlag(command.PayloadJson);
                    working = CloneState(working, showRemainingTime: show);
                    break;
                case RemoteCommandType.ClearTemporaryFiles:
                    ClearAppTemporaryFiles();
                    break;
                case RemoteCommandType.Restart:
                    if (_options.EnableDestructiveCommands)
                    {
                        Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false });
                    }
                    break;
                case RemoteCommandType.Logout:
                    if (_options.EnableDestructiveCommands)
                    {
                        Process.Start(new ProcessStartInfo("shutdown", "/l") { CreateNoWindow = true, UseShellExecute = false });
                    }
                    break;
                default:
                    break;
            }

            working = AppendNotifications(working,
            [
                new NotificationEnvelope(command.Id, command.Title, string.IsNullOrWhiteSpace(command.Message) ? "Comando recebido do servidor." : command.Message, NotificationSeverity.Info, true)
            ]);
        }

        await Task.CompletedTask;
        return working;
    }

    private static bool ParseShowFlag(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.TryGetProperty("show", out var property) && property.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (document.RootElement.TryGetProperty("show", out property) && property.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }
        catch
        {
            return true;
        }

        return true;
    }

    private async Task EnforceBlockedProgramsAsync(string blockedProgramsCsv)
    {
        var blocked = blockedProgramsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .ToHashSet();
        if (blocked.Count == 0)
        {
            return;
        }

        foreach (var process in Process.GetProcesses())
        {
            var executable = $"{process.ProcessName}.exe".ToLowerInvariant();
            if (blocked.Contains(executable))
            {
                try
                {
                    process.Kill(true);
                    logger.LogInformation("Processo bloqueado encerrado: {ProcessName}", executable);
                }
                catch (Exception exception)
                {
                    logger.LogDebug(exception, "Não foi possível encerrar {ProcessName}.", executable);
                }
            }
        }

        await Task.CompletedTask;
    }

    private static void ClearAppTemporaryFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Adrenalina");
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(directory))
        {
            File.Delete(file);
        }
    }

    private static string ResolveLocalIpAddress()
    {
        try
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?
                .ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static ClientRuntimeState AppendNotifications(ClientRuntimeState state, IReadOnlyList<NotificationEnvelope> notifications)
    {
        if (notifications.Count == 0)
        {
            return state;
        }

        var allNotifications = state.Notifications
            .Concat(notifications)
            .DistinctBy(item => item.Id)
            .TakeLast(12)
            .ToList();

        return CloneState(state, notifications: allNotifications);
    }

    private static ClientRuntimeState CloneState(
        ClientRuntimeState state,
        bool? isLocked = null,
        bool? showRemainingTime = null,
        string? sessionMessage = null,
        string? lockMessage = null,
        IReadOnlyList<NotificationEnvelope>? notifications = null)
    {
        return new ClientRuntimeState
        {
            MachineName = state.MachineName,
            Theme = state.Theme,
            IsLocked = isLocked ?? state.IsLocked,
            IsDemoMode = state.IsDemoMode,
            ShowRemainingTime = showRemainingTime ?? state.ShowRemainingTime,
            LockMessage = lockMessage ?? state.LockMessage,
            WelcomeMessage = state.WelcomeMessage,
            GoodbyeMessage = state.GoodbyeMessage,
            CurrentUserName = state.CurrentUserName,
            CurrentUserLogin = state.CurrentUserLogin,
            CurrentUserNotes = state.CurrentUserNotes,
            CurrentUserProfile = state.CurrentUserProfile,
            CurrentBalance = state.CurrentBalance,
            PendingAnnotations = state.PendingAnnotations,
            RemainingMinutes = state.RemainingMinutes,
            SessionMessage = sessionMessage ?? state.SessionMessage,
            LastUpdatedAtUtc = DateTime.UtcNow,
            Notifications = notifications ?? state.Notifications
        };
    }
}
