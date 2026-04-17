using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Adrenalina.Application;
using Adrenalina.Domain;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Client;

public sealed class ClientServerGateway(
    ClientConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    IClientRuntimeStore runtimeStore,
    ILogger<ClientServerGateway> logger)
{
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public string CurrentBlockedProgramsCsv { get; private set; } = "taskmgr.exe,regedit.exe,powershell.exe,cmd.exe";

    public async Task SyncOnceAsync(CancellationToken cancellationToken = default)
    {
        await _syncGate.WaitAsync(cancellationToken);
        try
        {
            await SyncCoreAsync(cancellationToken);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public async Task<ClientLoginResponse> LoginAsync(string login, string pin, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("adrenalina-server");

        try
        {
            var response = await client.PostAsJsonAsync(
                "api/client/login",
                new ClientLoginRequest
                {
                    MachineKey = options.MachineKey,
                    Login = login,
                    Pin = pin
                },
                JsonDefaults.Options,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<ClientLoginResponse>(JsonDefaults.Options, cancellationToken)
                          ?? new ClientLoginResponse
                          {
                              Success = false,
                              Message = "O servidor não retornou uma resposta de login válida."
                          };

            if (payload.Success)
            {
                await runtimeStore.SaveStateAsync(
                    AppendNotifications(
                        payload.RuntimeState,
                        [
                            new NotificationEnvelope(Guid.NewGuid(), "Sessão iniciada", payload.Message, NotificationSeverity.Success, true)
                        ]),
                    cancellationToken);
            }

            return payload;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Login online indisponível. Solicitação será enfileirada.");

            await runtimeStore.EnqueueRequestAsync(
                new ClientShellRequest
                {
                    Type = ClientRequestType.Login,
                    Login = login,
                    Pin = pin,
                    OccurredAtUtc = DateTime.UtcNow
                },
                cancellationToken);

            var state = await runtimeStore.LoadStateAsync(cancellationToken);
            await runtimeStore.SaveStateAsync(
                CloneState(
                    state,
                    sessionMessage: "Servidor offline. O pedido de login foi enfileirado para aprovação.",
                    notifications: state.Notifications
                        .Concat(
                        [
                            new NotificationEnvelope(Guid.NewGuid(), "Login pendente", "O servidor está offline. Seu login foi colocado na fila.", NotificationSeverity.Warning, true)
                        ])
                        .TakeLast(12)
                        .ToList()),
                cancellationToken);

            return new ClientLoginResponse
            {
                Success = false,
                Message = "Servidor offline. O pedido foi enfileirado.",
                RuntimeState = await runtimeStore.LoadStateAsync(cancellationToken)
            };
        }
    }

    public Task QueueRequestAsync(ClientShellRequest request, CancellationToken cancellationToken = default) =>
        runtimeStore.EnqueueRequestAsync(request, cancellationToken);

    private async Task SyncCoreAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("adrenalina-server");
        var queuedRequests = await runtimeStore.DrainRequestsAsync(cancellationToken);

        try
        {
            if (queuedRequests.Count > 0)
            {
                var requestResponse = await client.PostAsJsonAsync(
                    "api/client/requests",
                    new ClientRequestBatchRequest
                    {
                        MachineKey = options.MachineKey,
                        Requests = queuedRequests
                    },
                    JsonDefaults.Options,
                    cancellationToken);
                requestResponse.EnsureSuccessStatusCode();
            }

            var heartbeat = new ClientHeartbeatRequest
            {
                MachineKey = options.MachineKey,
                MachineName = options.MachineName,
                Hostname = Environment.MachineName,
                IpAddress = ResolveLocalIpAddress(),
                Kind = options.MachineKind,
                Status = await ResolveMachineStatusAsync(cancellationToken),
                Processes = CollectProcesses()
            };

            var response = await client.PostAsJsonAsync("api/client/heartbeat", heartbeat, JsonDefaults.Options, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ClientHeartbeatResponse>(JsonDefaults.Options, cancellationToken)
                          ?? new ClientHeartbeatResponse();

            CurrentBlockedProgramsCsv = payload.Settings.BlockedProgramsCsv;

            var updatedState = await ApplyCommandsAsync(payload.RuntimeState, payload.Commands, cancellationToken);
            updatedState = AppendNotifications(updatedState, payload.Notifications);
            await runtimeStore.SaveStateAsync(updatedState, cancellationToken);

            await EnforceBlockedProgramsAsync(CurrentBlockedProgramsCsv, updatedState.IsLocked, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Servidor indisponível. Cliente seguirá no modo offline.");
            foreach (var item in queuedRequests)
            {
                await runtimeStore.EnqueueRequestAsync(item, cancellationToken);
            }

            var current = await runtimeStore.LoadStateAsync(cancellationToken);
            await runtimeStore.SaveStateAsync(
                CloneState(current, sessionMessage: "Servidor offline. O cliente segue operando e tentará sincronizar novamente."),
                cancellationToken);
        }
    }

    private async Task<MachineStatus> ResolveMachineStatusAsync(CancellationToken cancellationToken)
    {
        var state = await runtimeStore.LoadStateAsync(cancellationToken);
        if (state.IsLocked)
        {
            return MachineStatus.Locked;
        }

        return state.CurrentSessionId.HasValue || !string.IsNullOrWhiteSpace(state.CurrentUserLogin)
            ? MachineStatus.InSession
            : MachineStatus.Idle;
    }

    private static IReadOnlyList<ProcessDto> CollectProcesses()
    {
        try
        {
            return Process.GetProcesses()
                .OrderByDescending(process => process.WorkingSet64)
                .Take(16)
                .Select(
                    process =>
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
                    working = CloneState(working, showRemainingTime: ParseShowFlag(command.PayloadJson));
                    break;
                case RemoteCommandType.ShowMessage:
                    break;
                case RemoteCommandType.ClearTemporaryFiles:
                    ClearAppTemporaryFiles();
                    break;
                case RemoteCommandType.Restart:
                    if (options.EnableDestructiveCommands)
                    {
                        Process.Start(new ProcessStartInfo("shutdown", "/r /t 0")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    else
                    {
                        working = CloneState(working, isLocked: true, sessionMessage: "Reinício solicitado pelo administrador.");
                    }

                    break;
                case RemoteCommandType.Logout:
                    if (options.EnableDestructiveCommands)
                    {
                        Process.Start(new ProcessStartInfo("shutdown", "/l")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    else
                    {
                        working = CloneState(working, isLocked: true, sessionMessage: "Logout solicitado pelo administrador.");
                    }

                    break;
            }

            working = AppendNotifications(
                working,
                [
                    new NotificationEnvelope(
                        command.Id,
                        string.IsNullOrWhiteSpace(command.Title) ? "Comando do administrador" : command.Title,
                        string.IsNullOrWhiteSpace(command.Message) ? "Uma ação remota foi recebida." : command.Message,
                        NotificationSeverity.Info,
                        true)
                ]);
        }

        await Task.CompletedTask;
        return working;
    }

    private async Task EnforceBlockedProgramsAsync(string blockedProgramsCsv, bool isLocked, CancellationToken cancellationToken)
    {
        var blocked = blockedProgramsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .ToHashSet();

        if (isLocked)
        {
            blocked.UnionWith(
            [
                "explorer.exe",
                "taskmgr.exe",
                "regedit.exe",
                "cmd.exe",
                "powershell.exe",
                "mmc.exe",
                "control.exe"
            ]);
        }

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var executable = $"{process.ProcessName}.exe".ToLowerInvariant();
                if (blocked.Contains(executable))
                {
                    process.Kill(true);
                }
            }
            catch
            {
                // O bloqueio roda em background e ignora processos críticos/negados pelo sistema.
            }
        }

        await Task.CompletedTask;
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
            if (document.RootElement.TryGetProperty("show", out var property))
            {
                return property.ValueKind != JsonValueKind.False;
            }
        }
        catch
        {
            return true;
        }

        return true;
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
            CurrentSessionId = state.CurrentSessionId,
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
