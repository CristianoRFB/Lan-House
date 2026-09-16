namespace Adrenalina.Client;

public sealed class ClientConnectionOptions
{
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string MachineKey { get; set; } = Environment.MachineName.ToLowerInvariant();
    public string MachineName { get; set; } = Environment.MachineName;
    public Guid? MachineId { get; set; }
    public string MachineCredentialId { get; set; } = string.Empty;
    public Guid? PairingSessionId { get; set; }
    public int SyncIntervalSeconds { get; set; } = 10;
    public bool SetupCompleted { get; set; }
    public bool ShowTutorialOnNextLaunch { get; set; }
    public int OnboardingVersion { get; set; } = 1;
    public bool RequireWindowsAgent { get; set; }
}
