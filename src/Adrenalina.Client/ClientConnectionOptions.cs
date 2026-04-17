using Adrenalina.Domain;

namespace Adrenalina.Client;

public sealed class ClientConnectionOptions
{
    public string ServerBaseUrl { get; set; } = "http://127.0.0.1:5076/";
    public string MachineKey { get; set; } = Environment.MachineName.ToLowerInvariant();
    public string MachineName { get; set; } = Environment.MachineName;
    public MachineKind MachineKind { get; set; } = MachineKind.Pc;
    public int SyncIntervalSeconds { get; set; } = 10;
    public bool EnableDestructiveCommands { get; set; } = true;
    public bool LaunchLocalWatchdog { get; set; } = true;
    public string UiScheduledTaskName { get; set; } = "Adrenalina Client UI";
}
