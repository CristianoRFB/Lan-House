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
    private readonly HashSet<Guid> _commandAcknowledgements = [];
    private readonly HashSet<Guid> _notificationAcknowledgements = [];

    public bool IsServerOnline { get; private set; }
    public string ConnectionStatusText { get; private set; } = "Conexão aguardando a primeira sincronização.";

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
        if (!IsRemoteServerConfigured())
        {
            SetConnectionStatus(false, "Informe o endereço do ADMIN para concluir a configuração do Client.");
            return new ClientLoginResponse
            {
                Success = false,
                Message = "Informe o IP do ADMIN antes de tentar entrar.",
                RuntimeState = await runtimeStore.LoadStateAsync(cancellationToken)
            };
        }

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

            SetConnectionStatus(true, $"Conectado ao servidor em {options.ServerBaseUrl}");

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
            logger.LogWarning(exception, "Login online indisponivel.");
            SetConnectionStatus(false, "Servidor offline. O login não pode ser validado agora.");

            var state = await runtimeStore.LoadStateAsync(cancellationToken);
            await runtimeStore.SaveStateAsync(
                CloneState(
                    state,
                    sessionMessage: "Servidor offline. Aguarde a reconexão para entrar.",
                    notifications: state.Notifications
                        .Concat(
                        [
                            new NotificationEnvelope(Guid.NewGuid(), "Servidor offline", "O login não foi enviado nem armazenado. Tente novamente quando o servidor voltar.", NotificationSeverity.Warning, true)
                        ])
                        .TakeLast(12)
                        .ToList()),
                cancellationToken);

            return new ClientLoginResponse
            {
                Success = false,
                Message = "Servidor offline. Não foi possível validar o login.",
                RuntimeState = await runtimeStore.LoadStateAsync(cancellationToken)
            };
        }
    }

    public Task QueueRequestAsync(ClientShellRequest request, CancellationToken cancellationToken = default)
    {
        var safeRequest = request.Type == ClientRequestType.Registration && LoginRules.LooksLikeFourDigitPin(request.Pin)
            ? new ClientShellRequest
            {
                RequestId = request.RequestId == Guid.Empty ? Guid.NewGuid() : request.RequestId,
                Type = request.Type,
                Login = request.Login,
                PinHash = PasswordHasher.Hash(request.Pin),
                DisplayName = request.DisplayName,
                Message = request.Message,
                Amount = request.Amount,
                OccurredAtUtc = request.OccurredAtUtc
            }
            : NormalizeRequestId(request);
        return runtimeStore.EnqueueRequestAsync(safeRequest, cancellationToken);
    }

    public async Task<OperationResult> TestConnectionAsync(string serverBaseUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(serverBaseUrl?.Trim(), UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return new OperationResult(false, "Informe uma URL HTTP ou HTTPS válida.");
        }

        try
        {
            using var client = new HttpClient
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var response = await client.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode
                ? new OperationResult(true, "Conexão com o servidor validada.")
                : new OperationResult(false, $"O servidor respondeu com HTTP {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OperationResult(false, "O servidor não respondeu dentro de 5 segundos.");
        }
        catch (Exception)
        {
            return new OperationResult(false, "Não foi possível conectar ao servidor informado.");
        }
    }

    private async Task SyncCoreAsync(CancellationToken cancellationToken)
    {
        if (!IsRemoteServerConfigured())
        {
            SetConnectionStatus(false, "Informe o endereço do ADMIN para concluir a configuração.");
            return;
        }

        var client = httpClientFactory.CreateClient("adrenalina-server");
        var queuedRequests = (await runtimeStore.DrainRequestsAsync(cancellationToken))
            .Select(NormalizeRequestId)
            .ToList();

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
                var requestResult = await requestResponse.Content.ReadFromJsonAsync<OperationResult>(JsonDefaults.Options, cancellationToken);
                if (requestResult is null || !requestResult.Success)
                {
                    throw new InvalidOperationException(requestResult?.Message ?? "O servidor rejeitou as solicitações pendentes.");
                }
            }

            var heartbeat = new ClientHeartbeatRequest
            {
                MachineKey = options.MachineKey,
                Hostname = Environment.MachineName,
                IpAddress = ResolveLocalIpAddress(),
                Status = await ResolveMachineStatusAsync(cancellationToken),
                AcknowledgedCommandIds = _commandAcknowledgements.ToList(),
                AcknowledgedNotificationIds = _notificationAcknowledgements.ToList()
            };

            var response = await client.PostAsJsonAsync("api/client/heartbeat", heartbeat, JsonDefaults.Options, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ClientHeartbeatResponse>(JsonDefaults.Options, cancellationToken)
                          ?? new ClientHeartbeatResponse();

            if (!payload.Success)
            {
                throw new InvalidOperationException(payload.Message);
            }

            SetConnectionStatus(true, $"Servidor online em {options.ServerBaseUrl}");

            var updatedState = ApplyCommands(payload.RuntimeState, payload.Commands);
            updatedState = AppendNotifications(updatedState, payload.Notifications);
            await runtimeStore.SaveStateAsync(updatedState, cancellationToken);

            _commandAcknowledgements.Clear();
            _notificationAcknowledgements.Clear();
            foreach (var command in payload.Commands)
            {
                _commandAcknowledgements.Add(command.Id);
            }
            foreach (var notification in payload.Notifications)
            {
                _notificationAcknowledgements.Add(notification.Id);
            }

        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Servidor indisponivel. Cliente seguira no modo offline.");
            SetConnectionStatus(false, "Servidor offline. O Client tentará sincronizar novamente.");

            foreach (var item in queuedRequests)
            {
                await runtimeStore.EnqueueRequestAsync(item, cancellationToken);
            }

            var current = await runtimeStore.LoadStateAsync(cancellationToken);
            await runtimeStore.SaveStateAsync(
                CloneState(current, sessionMessage: "Servidor offline. O Client tentará sincronizar novamente."),
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

    private ClientRuntimeState ApplyCommands(
        ClientRuntimeState state,
        IReadOnlyList<RemoteCommandEnvelope> commands)
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
            }

            working = AppendNotifications(
                working,
                [
                    new NotificationEnvelope(
                        command.Id,
                        string.IsNullOrWhiteSpace(command.Title) ? "Comando do administrador" : command.Title,
                        string.IsNullOrWhiteSpace(command.Message) ? "Uma acao remota foi recebida." : command.Message,
                        NotificationSeverity.Info,
                        true)
                ]);
        }

        return working;
    }

    private static ClientShellRequest NormalizeRequestId(ClientShellRequest request)
    {
        if (request.RequestId != Guid.Empty)
        {
            return request;
        }

        return new ClientShellRequest
        {
            RequestId = Guid.NewGuid(),
            Type = request.Type,
            Login = request.Login,
            Pin = request.Pin,
            PinHash = request.PinHash,
            DisplayName = request.DisplayName,
            Message = request.Message,
            Amount = request.Amount,
            OccurredAtUtc = request.OccurredAtUtc
        };
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

    private bool IsRemoteServerConfigured()
    {
        if (!options.SetupCompleted)
        {
            return false;
        }

        return Uri.TryCreate(options.ServerBaseUrl?.Trim(), UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
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

    private void SetConnectionStatus(bool isOnline, string message)
    {
        IsServerOnline = isOnline;
        ConnectionStatusText = message;
    }
}
