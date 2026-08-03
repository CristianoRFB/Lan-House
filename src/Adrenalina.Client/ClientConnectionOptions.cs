namespace Adrenalina.Client;

public sealed class ClientConnectionOptions
{
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string MachineKey { get; set; } = Environment.MachineName.ToLowerInvariant();
    public string MachineName { get; set; } = Environment.MachineName;
    public int SyncIntervalSeconds { get; set; } = 10;
    public bool SetupCompleted { get; set; }
    public bool ShowTutorialOnNextLaunch { get; set; }
}
