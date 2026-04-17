using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adrenalina.Domain;

namespace Adrenalina.Application;

public sealed record OperationResult(bool Success, string Message);

public sealed record AuthenticatedAdmin(Guid Id, string Login, string DisplayName, UserProfileType ProfileType);

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
    public IReadOnlyList<ProcessDto> RecentProcesses { get; init; } = [];
}

public sealed class ProcessDto
{
    public string ProcessName { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;
    public double MemoryMb { get; init; }
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
    public string MachineKey { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public MachineKind Kind { get; init; } = MachineKind.Pc;
    public MachineStatus Status { get; init; } = MachineStatus.Offline;
    public IReadOnlyList<ProcessDto> Processes { get; init; } = [];
}

public sealed class ClientHeartbeatResponse
{
    public Guid MachineId { get; init; }
    public SettingsDto Settings { get; init; } = new();
    public ClientRuntimeState RuntimeState { get; init; } = new();
    public IReadOnlyList<RemoteCommandEnvelope> Commands { get; init; } = [];
    public IReadOnlyList<NotificationEnvelope> Notifications { get; init; } = [];
}

public sealed class ClientLoginRequest
{
    public string MachineKey { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public string Pin { get; init; } = string.Empty;
}

public sealed class ClientLoginResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public ClientRuntimeState RuntimeState { get; init; } = new();
}

public sealed class ClientRequestBatchRequest
{
    public string MachineKey { get; init; } = string.Empty;
    public IReadOnlyList<ClientShellRequest> Requests { get; init; } = [];
}

public sealed class ClientShellRequest
{
    public ClientRequestType Type { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Pin { get; init; } = string.Empty;
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

public sealed record RemoteCommandEnvelope(Guid Id, RemoteCommandType Type, string Title, string Message, string PayloadJson);

public sealed record NotificationEnvelope(Guid Id, string Title, string Message, NotificationSeverity Severity, bool PlaySound);

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
    Task RunMaintenanceTickAsync(CancellationToken cancellationToken = default);
}

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static string Hash(string rawValue)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(rawValue, salt, Iterations, HashAlgorithmName.SHA512, KeySize);
        return string.Join('.', Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public static bool Verify(string hashedValue, string rawValue)
    {
        if (string.IsNullOrWhiteSpace(hashedValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var parts = hashedValue.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(rawValue, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
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
    public static bool LooksLikeFourDigitPin(string pin) => pin.Length == 4 && pin.All(char.IsDigit);

    public static bool LooksLikeLetterLogin(string login) =>
        !string.IsNullOrWhiteSpace(login) &&
        login.All(character => char.IsLetter(character) || character is '.' or '_' or '-');
}

public static class TextSanitizer
{
    public static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value.Trim()));
}
