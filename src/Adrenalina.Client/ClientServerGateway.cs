using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text.Json;
using Adrenalina.Application;
using Adrenalina.Domain;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Client;

public sealed class ClientServerGateway(
    ClientConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    IClientRuntimeStore runtimeStore,
    ILogger<ClientServerGateway> logger,
    ClientCredentialStore? credentialStore = null,
    IStationEnforcementService? stationEnforcement = null)
{
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly HashSet<Guid> _commandAcknowledgements = [];
    private readonly HashSet<Guid> _notificationAcknowledgements = [];

    public bool IsServerOnline { get; private set; }
    public string ConnectionStatusText { get; private set; } = "Conexão aguardando a primeira sincronização.";

    public async Task<ClientPairingResponse> RequestPairingAsync(string code, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(options.ServerBaseUrl?.Trim(), UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps) ||
            !IsPairingCodeValid(code))
        {
            return new ClientPairingResponse { Success = false, Message = "Informe uma conexão válida e os 6 números do código temporário do ADMIN." };
        }

        try
        {
            using var client = new HttpClient { BaseAddress = serverUri, Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.PostAsJsonAsync("api/client/pairing/request", new ClientPairingRequest
            {
                Code = code.Trim(),
                Hostname = Environment.MachineName,
                MachineFingerprint = Environment.MachineName
            }, JsonDefaults.Options, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<ClientPairingResponse>(JsonDefaults.Options, cancellationToken) ?? new();
            if (payload.PairingSessionId != Guid.Empty)
            {
                options.PairingSessionId = payload.PairingSessionId;
                ClientOptionsStore.Save(options);
            }

            return payload;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Falha ao solicitar pareamento.");
            return new ClientPairingResponse { Success = false, Message = DescribePairingFailure(exception, "solicitar") };
        }
    }

    public async Task<ClientPairingResponse> PollPairingAsync(string code, CancellationToken cancellationToken = default)
    {
        if (!options.PairingSessionId.HasValue)
        {
            return new ClientPairingResponse { Success = false, Message = "Inicie o pareamento antes de verificar a aprovação." };
        }

        if (!Uri.TryCreate(options.ServerBaseUrl?.Trim(), UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps) ||
            !IsPairingCodeValid(code))
        {
            return new ClientPairingResponse { Success = false, Message = "O endereço do ADMIN ou o código de 6 números não é válido." };
        }

        try
        {
            using var client = new HttpClient { BaseAddress = serverUri, Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.PostAsJsonAsync("api/client/pairing/poll", new ClientPairingPollRequest
            {
                PairingSessionId = options.PairingSessionId.Value,
                Code = code.Trim()
            }, JsonDefaults.Options, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<ClientPairingResponse>(JsonDefaults.Options, cancellationToken) ?? new();
            if (payload.Success && !string.IsNullOrWhiteSpace(payload.MachineSecret) && credentialStore is not null)
            {
                credentialStore.Save(payload.MachineCredentialId, payload.MachineSecret);
                options.MachineId = payload.MachineId;
                options.MachineCredentialId = payload.MachineCredentialId;
                options.MachineKey = string.Empty;
                options.SetupCompleted = true;
                options.RequireWindowsAgent = true;
                options.PairingSessionId = null;
                ClientOptionsStore.Save(options);
            }

            return payload;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Falha ao verificar pareamento.");
            return new ClientPairingResponse { Success = false, Message = DescribePairingFailure(exception, "verificar a aprovação") };
        }
    }

    private static bool IsPairingCodeValid(string? code) =>
        code is not null && code.Trim().Length == 6 && code.Trim().All(char.IsDigit);

    private static string DescribePairingFailure(Exception exception, string action)
    {
        if (exception is HttpRequestException { InnerException: AuthenticationException } ||
            exception.InnerException is AuthenticationException)
        {
            return "A conexão HTTPS do ADMIN não foi reconhecida. Copie o arquivo .cer de C:\\ProgramData\\Adrenalina\\certs no ADMIN e use INSTALAR CERTIFICADO DO ADMIN nesta tela.";
        }

        if (exception is TaskCanceledException)
        {
            return $"O ADMIN demorou para responder. Confirme o endereço, a rede e tente {action} novamente.";
        }

        return $"Não foi possível {action} o pareamento. Confirme o endereço HTTPS exibido no ADMIN, a rede e o firewall.";
    }

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
            var (loginTimestamp, loginNonce, loginProof) = CreateMachineProof("login");
            var response = await client.PostAsJsonAsync(
                "api/client/login",
                new ClientLoginRequest
                {
                    MachineKey = options.MachineKey,
                    MachineId = options.MachineId,
                    MachineCredentialId = options.MachineCredentialId,
                    RequestTimestampUtc = loginTimestamp,
                    Nonce = loginNonce,
                    MachineProof = loginProof,
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
                    CreateRequestBatch(queuedRequests),
                    JsonDefaults.Options,
                    cancellationToken);
                requestResponse.EnsureSuccessStatusCode();
                var requestResult = await requestResponse.Content.ReadFromJsonAsync<OperationResult>(JsonDefaults.Options, cancellationToken);
                if (requestResult is null || !requestResult.Success)
                {
                    throw new InvalidOperationException(requestResult?.Message ?? "O servidor rejeitou as solicitações pendentes.");
                }
            }

            var (heartbeatTimestamp, heartbeatNonce, heartbeatProof) = CreateMachineProof("heartbeat");
            var heartbeat = new ClientHeartbeatRequest
            {
                MachineKey = options.MachineKey,
                MachineId = options.MachineId,
                MachineCredentialId = options.MachineCredentialId,
                RequestTimestampUtc = heartbeatTimestamp,
                Nonce = heartbeatNonce,
                MachineProof = heartbeatProof,
                Hostname = Environment.MachineName,
                IpAddress = ResolveLocalIpAddress(),
                ClientVersion = typeof(ClientServerGateway).Assembly.GetName().Version?.ToString() ?? "dev",
                AgentVersion = "named-pipe-v1",
                AgentHealthy = stationEnforcement?.HealthCheck() == true,
                PolicyVersion = 1,
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
                try
                {
                    // The request was drained before the network call. Restore it even when
                    // the transport cancellation token has already been signaled.
                    await runtimeStore.EnqueueRequestAsync(item, CancellationToken.None);
                }
                catch (Exception restoreException)
                {
                    logger.LogError(restoreException, "Não foi possível restaurar uma solicitação pendente na fila local.");
                }
            }

            var current = await runtimeStore.LoadStateAsync(cancellationToken);
            await runtimeStore.SaveStateAsync(
                CloneState(current, sessionMessage: "Servidor offline. O Client tentará sincronizar novamente."),
                cancellationToken);
        }
    }

    private ClientRequestBatchRequest CreateRequestBatch(IReadOnlyList<ClientShellRequest> requests)
    {
        var (timestamp, nonce, proof) = CreateMachineProof("requests");
        return new ClientRequestBatchRequest
        {
            MachineKey = options.MachineKey,
            MachineId = options.MachineId,
            MachineCredentialId = options.MachineCredentialId,
            RequestTimestampUtc = timestamp,
            Nonce = nonce,
            MachineProof = proof,
            Requests = requests
        };
    }

    private (DateTime Timestamp, string Nonce, string Proof) CreateMachineProof(string operation)
    {
        var timestamp = DateTime.UtcNow;
        var nonce = Guid.NewGuid().ToString("N");
        var secret = credentialStore?.Load(options.MachineCredentialId) ?? options.MachineKey;
        return (timestamp, nonce, MachineAuthentication.CreateProof(secret, timestamp, nonce, operation));
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
                    stationEnforcement?.ApplySessionState(false);
                    break;
                case RemoteCommandType.UnlockStation:
                    working = CloneState(working, isLocked: false, sessionMessage: command.Message);
                    stationEnforcement?.ApplySessionState(true);
                    break;
                case RemoteCommandType.EnterMaintenance:
                    working = CloneState(working, isLocked: false, sessionMessage: "Modo de manutenção autorizado.");
                    stationEnforcement?.EnterMaintenance();
                    break;
                case RemoteCommandType.ExitMaintenance:
                    working = CloneState(working, isLocked: true, sessionMessage: "Manutenção encerrada.");
                    stationEnforcement?.ExitMaintenance();
                    break;
                case RemoteCommandType.RestartStation:
                    stationEnforcement?.Restart();
                    break;
                case RemoteCommandType.ShutdownStation:
                    stationEnforcement?.Shutdown();
                    break;
                case RemoteCommandType.LogoffStation:
                    stationEnforcement?.Logoff();
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
