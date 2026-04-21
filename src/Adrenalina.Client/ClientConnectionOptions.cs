using Adrenalina.Domain;

namespace Adrenalina.Client;

public sealed class ClientConnectionOptions
{
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string MachineKey { get; set; } = Environment.MachineName.ToLowerInvariant();
    public string MachineName { get; set; } = Environment.MachineName;
    public MachineKind MachineKind { get; set; } = MachineKind.Pc;
    public int SyncIntervalSeconds { get; set; } = 10;
    public bool EnableDestructiveCommands { get; set; }
    public bool LaunchLocalWatchdog { get; set; }
    public string UiScheduledTaskName { get; set; } = "Adrenalina Client UI";
    public bool SetupCompleted { get; set; }
    public bool ShowTutorialOnNextLaunch { get; set; }
}
