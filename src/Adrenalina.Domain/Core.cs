namespace Adrenalina.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}

public enum UserProfileType
{
    Admin = 1,
    Ghost = 2,
    Special = 3,
    Common = 4
}

public enum MachineKind
{
    Pc = 1,
    Console = 2
}

public enum MachineStatus
{
    Offline = 1,
    Idle = 2,
    InSession = 3,
    Locked = 4,
    Maintenance = 5
}

public enum SessionStatus
{
    Active = 1,
    Paused = 2,
    Finished = 3,
    Expired = 4
}

public enum LedgerEntryType
{
    Credit = 1,
    Annotation = 2,
    PaymentPromise = 3,
    Adjustment = 4
}

public enum NotificationSeverity
{
    Info = 1,
    Success = 2,
    Warning = 3,
    Critical = 4
}

public enum RemoteCommandType
{
    LockScreen = 1,
    Restart = 2,
    Logout = 3,
    CaptureScreenshot = 4,
    ClearTemporaryFiles = 5,
    RefreshConfiguration = 6,
    ShowMessage = 7,
    ToggleTimerVisibility = 8
}

public enum RemoteCommandStatus
{
    Pending = 1,
    Delivered = 2,
    Completed = 3,
    Failed = 4
}

public enum ClientRequestType
{
    Login = 1,
    MoreTime = 2,
    Registration = 3,
    ApplicationInstall = 4,
    Annotation = 5
}

public enum ClientRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum UpdateMode
{
    Manual = 1,
    Automatic = 2
}

public enum ThemeMode
{
    Dark = 1,
    Light = 2
}

public enum ReportExportFormat
{
    Pdf = 1,
    Excel = 2,
    Txt = 3
}

public sealed class AdminSettings : Entity
{
    public string CafeName { get; set; } = "Adrenalina Lan House";
    public ThemeMode DefaultTheme { get; set; } = ThemeMode.Dark;
    public UpdateMode UpdateMode { get; set; } = UpdateMode.Automatic;
    public TimeOnly BackupCutoffLocalTime { get; set; } = new(20, 30);
    public int BackupRetentionDays { get; set; } = 60;
    public string WelcomeMessage { get; set; } = "Bem-vindo, {usuario}.";
    public string GoodbyeMessage { get; set; } = "Obrigado por usar a Lan House.";
    public string LockMessage { get; set; } = "Seu tempo terminou. Procure o atendimento para continuar.";
    public string AllowedProgramsCsv { get; set; } = string.Empty;
    public string BlockedProgramsCsv { get; set; } = string.Empty;
    public bool LimitBandwidthEnabledByDefault { get; set; }
    public bool OfflineSyncEnabled { get; set; } = true;
    public bool ShowRemainingTimeByDefault { get; set; } = true;
    public decimal DefaultCommonAnnotationLimit { get; set; } = 25m;
    public decimal DefaultPcHourlyRate { get; set; } = 12m;
    public decimal DefaultConsoleHourlyRate { get; set; } = 15m;
    public bool DemoModeEnabled { get; set; } = true;
    public string BrandLogoPath { get; set; } = string.Empty;
    public string AlertSoundPath { get; set; } = string.Empty;
    public string AdminPanelPasswordHint { get; set; } = string.Empty;
    public bool AdvancedSettingsProtected { get; set; } = true;
}

public sealed class UserAccount : Entity
{
    public string DisplayName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserProfileType ProfileType { get; set; } = UserProfileType.Common;
    public decimal Balance { get; set; }
    public decimal PendingAnnotationAmount { get; set; }
    public decimal AnnotationLimit { get; set; }
    public bool IsTemporary { get; set; }
    public DateTime? TemporaryUntilUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool CanSeeOwnBalance { get; set; } = true;
    public bool CanSeeOwnAnnotations { get; set; } = true;

    public bool CanAccessAdminPanel => ProfileType is UserProfileType.Admin or UserProfileType.Special;
    public bool AllowsNegativeBalance => ProfileType is UserProfileType.Admin or UserProfileType.Special;
    public bool HasUnlimitedAnnotations => ProfileType is UserProfileType.Admin or UserProfileType.Special;
    public bool HasUnlimitedTime => ProfileType is UserProfileType.Admin or UserProfileType.Special or UserProfileType.Ghost;
}

public sealed class Machine : Entity
{
    public string MachineKey { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public MachineKind Kind { get; set; } = MachineKind.Pc;
    public MachineStatus Status { get; set; } = MachineStatus.Offline;
    public string GroupName { get; set; } = "Principal";
    public string OperatingSystem { get; set; } = "Windows 10";
    public bool ServiceProtectionEnabled { get; set; } = true;
    public bool AutoStartEnabled { get; set; } = true;
    public bool BandwidthLimitEnabled { get; set; }
    public int? BandwidthLimitKbps { get; set; }
    public Guid? CurrentSessionId { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public string LastCommandSummary { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
}

public sealed class SessionRecord : Entity
{
    public Guid MachineId { get; set; }
    public Guid? UserAccountId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public UserProfileType UserProfileType { get; set; } = UserProfileType.Common;
    public MachineKind MachineKind { get; set; } = MachineKind.Pc;
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastTickedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int GrantedMinutes { get; set; }
    public int RemainingMinutes { get; set; }
    public int ConsumedMinutes { get; set; }
    public int IdleMinutes { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal PendingAnnotationAmount { get; set; }
    public bool IsDemoMode { get; set; }
    public bool HideTimerOnClient { get; set; }
    public string TriggeredAlertsCsv { get; set; } = string.Empty;
    public string ClosureReason { get; set; } = string.Empty;
    public string LockMessage { get; set; } = string.Empty;
    public bool IsBillingSettled { get; set; }

    public bool CountsDownTime => !IsDemoMode && UserProfileType == UserProfileType.Common;
}

public sealed class LedgerEntry : Entity
{
    public Guid UserAccountId { get; set; }
    public LedgerEntryType Type { get; set; } = LedgerEntryType.Adjustment;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? PromisedPaymentDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class RemoteCommand : Entity
{
    public Guid MachineId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public RemoteCommandType Type { get; set; } = RemoteCommandType.ShowMessage;
    public RemoteCommandStatus Status { get; set; } = RemoteCommandStatus.Pending;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAtUtc { get; set; }
    public string ResultSummary { get; set; } = string.Empty;
}

public sealed class NotificationRecord : Entity
{
    public Guid? MachineId { get; set; }
    public Guid? UserAccountId { get; set; }
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool PlaySound { get; set; } = true;
    public bool IsVisibleToClient { get; set; } = true;
    public bool IsReadByAdmin { get; set; }
    public bool IsReadByClient { get; set; }
}

public sealed class ClientRequestRecord : Entity
{
    public Guid MachineId { get; set; }
    public Guid? UserAccountId { get; set; }
    public ClientRequestType Type { get; set; } = ClientRequestType.Login;
    public ClientRequestStatus Status { get; set; } = ClientRequestStatus.Pending;
    public string RequestedLogin { get; set; } = string.Empty;
    public string RequestedDisplayName { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string AdminResponse { get; set; } = string.Empty;
}

public sealed class BackupSnapshot : Entity
{
    public string FolderPath { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AuditLog : Entity
{
    public string Category { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public Guid? MachineId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}

public sealed class MachineProcessSnapshot : Entity
{
    public Guid MachineId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public double MemoryMb { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
