using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adrenalina.Domain;

namespace Adrenalina.Application;

public static class ProtocolContract
{
    public const int CurrentVersion = 2;
    public const int MinimumSupportedVersion = 1;
}

public static class MachineAuthentication
{
    public static string DeriveSigningKey(string machineSecret) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(machineSecret)));

    public static string CreateProof(string machineKey, DateTime timestampUtc, string nonce, string operation)
        => CreateProofWithSigningKey(DeriveSigningKey(machineKey), timestampUtc, nonce, operation);

    public static string CreateProofWithSigningKey(string signingKey, DateTime timestampUtc, string nonce, string operation)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var payload = $"{operation}\n{timestampUtc.Ticks}\n{nonce}";
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    public static bool VerifyProof(string machineKey, DateTime timestampUtc, string nonce, string operation, string proof)
        => VerifyProofWithSigningKey(DeriveSigningKey(machineKey), timestampUtc, nonce, operation, proof);

    public static bool VerifyProofWithSigningKey(string signingKey, DateTime timestampUtc, string nonce, string operation, string proof)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(proof) || nonce.Length > 100 || proof.Length > 200 ||
            timestampUtc.Kind != DateTimeKind.Utc || Math.Abs((DateTime.UtcNow - timestampUtc).TotalMinutes) > 2)
        {
            return false;
        }

        try
        {
            var expected = Convert.FromBase64String(CreateProofWithSigningKey(signingKey, timestampUtc, nonce, operation));
            var supplied = Convert.FromBase64String(proof);
            return CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record OperationResult(bool Success, string Message);

public sealed record DatabaseIntegrityResult(bool Healthy, string Detail, DateTime CheckedAtUtc);
public sealed record BackupValidationResult(bool Valid, string Detail, DateTime CheckedAtUtc);

public sealed record AuthenticatedAdmin(Guid Id, string Login, string DisplayName, UserProfileType ProfileType);
public sealed record AdminAccessRecoveryResult(string TemporaryPassword, string AccessFilePath);

public sealed class DashboardDto
{
    public string CafeName { get; init; } = string.Empty;
    public int OnlineMachines { get; init; }
    public int ActiveMachines { get; init; }
    public int ActiveSessions { get; init; }
    public int PendingRequests { get; init; }
    public decimal PendingAnnotations { get; init; }
    public decimal PromisedPayments { get; init; }
    public IReadOnlyList<MachineDto> Machines { get; init; } = [];
    public IReadOnlyList<UserDto> Users { get; init; } = [];
    public IReadOnlyList<SessionDto> Sessions { get; init; } = [];
    public IReadOnlyList<ClientRequestDto> Requests { get; init; } = [];
    public IReadOnlyList<AuditLogDto> Logs { get; init; } = [];
    public IReadOnlyList<ChartPointDto> UsageByDay { get; init; } = [];
    public IReadOnlyList<ChartPointDto> UsageByMachine { get; init; } = [];
}

public sealed record ChartPointDto(string Label, decimal Value);

public sealed class MachineDto
{
    public Guid Id { get; init; }
    public string MachineKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public MachineKind Kind { get; init; }
    public MachineStatus Status { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public bool ServiceProtectionEnabled { get; init; }
    public bool BandwidthLimitEnabled { get; init; }
    public int? BandwidthLimitKbps { get; init; }
    public string LastCommandSummary { get; init; } = string.Empty;
    public string Observations { get; init; } = string.Empty;
    public DateTime? LastSeenUtc { get; init; }
    public string MachineCredentialId { get; init; } = string.Empty;
    public int MachineCredentialVersion { get; init; }
    public bool IsRevoked { get; init; }
    public string ClientVersion { get; init; } = string.Empty;
    public string AgentVersion { get; init; } = string.Empty;
    public int ProtocolVersion { get; init; }
    public bool AgentHealthy { get; init; }
    public DateTime? LastAgentSeenUtc { get; init; }
    public int PolicyVersion { get; init; }
}

public sealed class UserDto
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public UserProfileType ProfileType { get; init; }
    public decimal Balance { get; init; }
    public decimal PendingAnnotationAmount { get; init; }
    public decimal AnnotationLimit { get; init; }
    public bool IsTemporary { get; init; }
    public DateTime? TemporaryUntilUtc { get; init; }
    public string Notes { get; init; } = string.Empty;
    public bool IsBlocked { get; init; }
}

public sealed class SessionDto
{
    public Guid Id { get; init; }
    public Guid MachineId { get; init; }
    public Guid? UserAccountId { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string UserDisplayName { get; init; } = string.Empty;
    public UserProfileType UserProfileType { get; init; }
    public MachineKind MachineKind { get; init; }
    public SessionStatus Status { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
    public int GrantedMinutes { get; init; }
    public int RemainingMinutes { get; init; }
    public int ConsumedMinutes { get; init; }
    public int IdleMinutes { get; init; }
    public decimal HourlyRate { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal PendingAnnotationAmount { get; init; }
    public bool IsDemoMode { get; init; }
    public bool HideTimerOnClient { get; init; }
}

public sealed class ClientRequestDto
{
    public Guid Id { get; init; }
    public Guid MachineId { get; init; }
    public Guid? UserAccountId { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public ClientRequestType Type { get; init; }
    public ClientRequestStatus Status { get; init; }
    public string RequestedLogin { get; init; } = string.Empty;
    public string RequestedDisplayName { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public DateTime RequestedAtUtc { get; init; }
    public string AdminResponse { get; init; } = string.Empty;
}

public sealed class AuditLogDto
{
    public DateTime CreatedAtUtc { get; init; }
    public string Category { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
}

public sealed class SettingsDto
{
    public Guid Id { get; init; }
    public string CafeName { get; init; } = string.Empty;
    public ThemeMode DefaultTheme { get; init; }
    public UpdateMode UpdateMode { get; init; }
    public TimeOnly BackupCutoffLocalTime { get; init; }
    public int BackupRetentionDays { get; init; }
    public string WelcomeMessage { get; init; } = string.Empty;
    public string GoodbyeMessage { get; init; } = string.Empty;
    public string LockMessage { get; init; } = string.Empty;
    public string AllowedProgramsCsv { get; init; } = string.Empty;
    public string BlockedProgramsCsv { get; init; } = string.Empty;
    public bool LimitBandwidthEnabledByDefault { get; init; }
    public bool OfflineSyncEnabled { get; init; }
    public bool ShowRemainingTimeByDefault { get; init; }
    public decimal DefaultCommonAnnotationLimit { get; init; }
    public decimal DefaultPcHourlyRate { get; init; }
    public decimal DefaultConsoleHourlyRate { get; init; }
    public bool DemoModeEnabled { get; init; }
    public string BrandLogoPath { get; init; } = string.Empty;
    public string AlertSoundPath { get; init; } = string.Empty;
}

public sealed class UserUpsertRequest
{
    public Guid? Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public string? Pin { get; init; }
    public string? Password { get; init; }
    public UserProfileType ProfileType { get; init; } = UserProfileType.Common;
    public decimal Balance { get; init; }
    public decimal AnnotationLimit { get; init; }
    public bool IsTemporary { get; init; }
    public DateTime? TemporaryUntilUtc { get; init; }
    public string Notes { get; init; } = string.Empty;
    public bool IsBlocked { get; init; }
}

public sealed class MachineUpsertRequest
{
    public Guid? Id { get; init; }
    public string MachineKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MachineKind Kind { get; init; } = MachineKind.Pc;
    public string GroupName { get; init; } = "Principal";
    public string Observations { get; init; } = string.Empty;
}

public sealed class MachinePairingStartRequest
{
    public Guid MachineId { get; init; }
}

public sealed class MachinePairingSessionDto
{
    public Guid Id { get; init; }
    public Guid MachineId { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime? RequestedAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string MachineFingerprint { get; init; } = string.Empty;
}

public sealed class ClientPairingRequest
{
    public int ProtocolVersion { get; init; } = ProtocolContract.CurrentVersion;
    public string Code { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string MachineFingerprint { get; init; } = string.Empty;
}

public sealed class ClientPairingPollRequest
{
    public int ProtocolVersion { get; init; } = ProtocolContract.CurrentVersion;
    public Guid PairingSessionId { get; init; }
    public string Code { get; init; } = string.Empty;
}

public sealed class ClientPairingResponse
{
    public bool Success { get; init; }
    public bool AwaitingApproval { get; init; }
    public string Message { get; init; } = string.Empty;
    public Guid PairingSessionId { get; init; }
    public Guid MachineId { get; init; }
    public string MachineCredentialId { get; init; } = string.Empty;
    public string MachineSecret { get; init; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; init; }
}

public sealed class LedgerEntryRequest
{
    public Guid UserAccountId { get; init; }
    public LedgerEntryType Type { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime? PromisedPaymentDateUtc { get; init; }
}

public sealed class SessionStartRequest
{
    public Guid MachineId { get; init; }
    public Guid? UserAccountId { get; init; }
    public string UserDisplayName { get; init; } = string.Empty;
    public int GrantedMinutes { get; init; }
    public decimal HourlyRate { get; init; }
    public bool IsDemoMode { get; init; }
    public bool HideTimerOnClient { get; init; }
}

public sealed class SessionAdjustRequest
{
    public Guid SessionId { get; init; }
    public int AdditionalMinutes { get; init; }
    public decimal AdditionalAnnotationAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class MachineCommandRequest
{
    public Guid MachineId { get; init; }
    public RemoteCommandType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
}

public sealed class ClientRequestResolution
{
    public Guid RequestId { get; init; }
    public bool Approve { get; init; }
    public string ResponseMessage { get; init; } = string.Empty;
}

public sealed class SettingsUpdateRequest
{
    public Guid Id { get; init; }
    public string CafeName { get; init; } = string.Empty;
    public ThemeMode DefaultTheme { get; init; }
    public UpdateMode UpdateMode { get; init; }
    public TimeOnly BackupCutoffLocalTime { get; init; }
    public int BackupRetentionDays { get; init; }
    public string WelcomeMessage { get; init; } = string.Empty;
    public string GoodbyeMessage { get; init; } = string.Empty;
    public string LockMessage { get; init; } = string.Empty;
    public string AllowedProgramsCsv { get; init; } = string.Empty;
    public string BlockedProgramsCsv { get; init; } = string.Empty;
    public bool LimitBandwidthEnabledByDefault { get; init; }
    public bool OfflineSyncEnabled { get; init; }
    public bool ShowRemainingTimeByDefault { get; init; }
    public decimal DefaultCommonAnnotationLimit { get; init; }
    public decimal DefaultPcHourlyRate { get; init; }
    public decimal DefaultConsoleHourlyRate { get; init; }
    public bool DemoModeEnabled { get; init; }
    public string BrandLogoPath { get; init; } = string.Empty;
    public string AlertSoundPath { get; init; } = string.Empty;
}

public sealed class ReportFilterRequest
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public ReportExportFormat Format { get; init; } = ReportExportFormat.Txt;
}

public sealed record FileExportResult(string FileName, string ContentType, byte[] Content);

public sealed class ClientHeartbeatRequest
{
    public int ProtocolVersion { get; init; } = ProtocolContract.CurrentVersion;
    public string MachineKey { get; init; } = string.Empty;
    public Guid? MachineId { get; init; }
    public string MachineCredentialId { get; init; } = string.Empty;
    public DateTime RequestTimestampUtc { get; init; }
    public string Nonce { get; init; } = string.Empty;
    public string MachineProof { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string ClientVersion { get; init; } = string.Empty;
    public string AgentVersion { get; init; } = string.Empty;
    public bool AgentHealthy { get; init; }
    public int PolicyVersion { get; init; }
    public MachineStatus Status { get; init; } = MachineStatus.Offline;
    public IReadOnlyList<Guid> AcknowledgedCommandIds { get; init; } = [];
    public IReadOnlyList<Guid> AcknowledgedNotificationIds { get; init; } = [];
}

public sealed class ClientHeartbeatResponse
{
    public int ProtocolVersion { get; init; } = ProtocolContract.CurrentVersion;
    public bool Success { get; init; } = true;
    public string Message { get; init; } = string.Empty;
    public Guid MachineId { get; init; }
    public SettingsDto Settings { get; init; } = new();
    public ClientRuntimeState RuntimeState { get; init; } = new();
    public IReadOnlyList<RemoteCommandEnvelope> Commands { get; init; } = [];
    public IReadOnlyList<NotificationEnvelope> Notifications { get; init; } = [];
}

public sealed class ClientLoginRequest
{
    public int ProtocolVersion { get; init; } = ProtocolContract.CurrentVersion;
    public string MachineKey { get; init; } = string.Empty;
    public Guid? MachineId { get; init; }
    public string MachineCredentialId { get; init; } = string.Empty;
    public DateTime RequestTimestampUtc { get; init; }
    public string Nonce { get; init; } = string.Empty;
    public string MachineProof { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public string Pin { get; init; } = string.Empty;
}

public sealed class ClientLoginResponse
{
    public int ProtocolVersion { get; init; } = ProtocolContract.CurrentVersion;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public ClientRuntimeState RuntimeState { get; init; } = new();
}

public sealed class ClientRequestBatchRequest
{
    public int ProtocolVersion { get; init; } = ProtocolContract.CurrentVersion;
    public string MachineKey { get; init; } = string.Empty;
    public Guid? MachineId { get; init; }
    public string MachineCredentialId { get; init; } = string.Empty;
    public DateTime RequestTimestampUtc { get; init; }
    public string Nonce { get; init; } = string.Empty;
    public string MachineProof { get; init; } = string.Empty;
    public IReadOnlyList<ClientShellRequest> Requests { get; init; } = [];
}

public sealed class ClientShellRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public ClientRequestType Type { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Pin { get; init; } = string.Empty;
    public string PinHash { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class ClientRuntimeState
{
    public string MachineName { get; init; } = string.Empty;
    public Guid? CurrentSessionId { get; init; }
    public ThemeMode Theme { get; init; } = ThemeMode.Dark;
    public bool IsLocked { get; init; }
    public bool IsDemoMode { get; init; }
    public bool ShowRemainingTime { get; init; } = true;
    public string LockMessage { get; init; } = string.Empty;
    public string WelcomeMessage { get; init; } = string.Empty;
    public string GoodbyeMessage { get; init; } = string.Empty;
    public string CurrentUserName { get; init; } = string.Empty;
    public string CurrentUserLogin { get; init; } = string.Empty;
    public string CurrentUserNotes { get; init; } = string.Empty;
    public UserProfileType CurrentUserProfile { get; init; } = UserProfileType.Ghost;
    public decimal CurrentBalance { get; init; }
    public decimal PendingAnnotations { get; init; }
    public int RemainingMinutes { get; init; }
    public string SessionMessage { get; init; } = string.Empty;
    public DateTime LastUpdatedAtUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<NotificationEnvelope> Notifications { get; init; } = [];
}

public sealed record RemoteCommandEnvelope(Guid Id, RemoteCommandType Type, string Title, string Message, string PayloadJson, DateTime? ExpiresAtUtc = null);

public sealed record NotificationEnvelope(Guid Id, string Title, string Message, NotificationSeverity Severity, bool PlaySound);

public static class StationAgentProtocol
{
    public const string PipeName = "Adrenalina.Agent";
    public const string Version = "v1";
}

public sealed class StationAgentRequest
{
    public string Protocol { get; init; } = StationAgentProtocol.Version;
    public string Action { get; init; } = string.Empty;
    public Guid? MachineId { get; init; }
    public bool SessionActive { get; init; }
    public DateTime IssuedAtUtc { get; init; }
    public string Nonce { get; init; } = string.Empty;
    public string Proof { get; init; } = string.Empty;
}

public sealed record StationAgentResponse(bool Success, bool Healthy, string Message);

public sealed class LocalClientStoragePaths
{
    public string StateFilePath { get; init; } = string.Empty;
    public string RequestQueueFilePath { get; init; } = string.Empty;
}

public interface IClientRuntimeStore
{
    Task<ClientRuntimeState> LoadStateAsync(CancellationToken cancellationToken = default);
    Task SaveStateAsync(ClientRuntimeState state, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientShellRequest>> DrainRequestsAsync(CancellationToken cancellationToken = default);
    Task EnqueueRequestAsync(ClientShellRequest request, CancellationToken cancellationToken = default);
}

public interface IAdminAuthService
{
    Task<AuthenticatedAdmin?> ValidateAsync(string login, string password, CancellationToken cancellationToken = default);
    Task<AdminAccessRecoveryResult?> RecoverAdminAccessAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> ChangeAdminPasswordAsync(Guid adminId, string newPassword, CancellationToken cancellationToken = default);
    Task<UserDto?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface ICafeManagementService
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MachineDto>> GetMachinesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientRequestDto>> GetPendingRequestsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogDto>> GetRecentLogsAsync(int take, CancellationToken cancellationToken = default);
    Task<SettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> SaveSettingsAsync(SettingsUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> UpsertUserAsync(UserUpsertRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> UpsertMachineAsync(MachineUpsertRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> RevokeMachineAsync(Guid machineId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<MachinePairingSessionDto?> StartMachinePairingAsync(MachinePairingStartRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<MachinePairingSessionDto?> GetMachinePairingAsync(Guid pairingSessionId, CancellationToken cancellationToken = default);
    Task<OperationResult> ApproveMachinePairingAsync(Guid pairingSessionId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<ClientPairingResponse> RequestMachinePairingAsync(ClientPairingRequest request, CancellationToken cancellationToken = default);
    Task<ClientPairingResponse> PollMachinePairingAsync(ClientPairingPollRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> AddLedgerEntryAsync(LedgerEntryRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> StartSessionAsync(SessionStartRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> AdjustSessionAsync(SessionAdjustRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> EndSessionAsync(Guid sessionId, string reason, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> QueueMachineCommandAsync(MachineCommandRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> ResolveClientRequestAsync(ClientRequestResolution request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> CreateManualBackupAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task<FileExportResult?> ExportReportAsync(ReportFilterRequest request, CancellationToken cancellationToken = default);
    Task<ClientHeartbeatResponse> SyncClientHeartbeatAsync(ClientHeartbeatRequest request, CancellationToken cancellationToken = default);
    Task<ClientLoginResponse> LoginClientAsync(ClientLoginRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> SubmitClientRequestsAsync(ClientRequestBatchRequest request, CancellationToken cancellationToken = default);
    Task<DatabaseIntegrityResult> CheckDatabaseIntegrityAsync(CancellationToken cancellationToken = default);
    Task<BackupValidationResult> ValidateBackupAsync(string backupPath, CancellationToken cancellationToken = default);
    Task RunMaintenanceTickAsync(CancellationToken cancellationToken = default);
}

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const int MaximumAcceptedIterations = 1_000_000;

    public static string Hash(string rawValue)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(rawValue, salt, Iterations, HashAlgorithmName.SHA512, KeySize);
        return string.Join('.', Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public static bool Verify(string hashedValue, string rawValue)
    {
        if (!IsHashFormatValid(hashedValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var parts = hashedValue.Split('.', 3);
        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(rawValue, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool IsHashFormatValid(string hashedValue)
    {
        if (string.IsNullOrWhiteSpace(hashedValue))
        {
            return false;
        }

        try
        {
            var parts = hashedValue.Split('.', 3);
            return parts.Length == 3 &&
                   int.TryParse(parts[0], out var iterations) &&
                   iterations is >= 10_000 and <= MaximumAcceptedIterations &&
                   Convert.FromBase64String(parts[1]).Length is >= 16 and <= 64 &&
                   Convert.FromBase64String(parts[2]).Length is >= 32 and <= 64;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public static class LoginRules
{
    public static bool LooksLikeFourDigitPin(string pin) =>
        pin is { Length: 4 } && pin.All(character => character is >= '0' and <= '9');

    public static bool LooksLikeLetterLogin(string login) =>
        !string.IsNullOrWhiteSpace(login) &&
        login.Length <= 64 &&
        login.All(character => char.IsLetter(character) || character is '.' or '_' or '-');
}

public static class TextSanitizer
{
    public static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim()
                .Where(character => !char.IsControl(character) || character is '\r' or '\n' or '\t')
                .Take(1_000)
                .ToArray());
}
