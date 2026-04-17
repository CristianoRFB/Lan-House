using Adrenalina.Domain;

namespace Adrenalina.ClientAgent;

public sealed class ClientAgentOptions
{
    public string ServerBaseUrl { get; set; } = "https://localhost:5001/";
    public string MachineKey { get; set; } = Environment.MachineName.ToLowerInvariant();
    public string MachineName { get; set; } = Environment.MachineName;
    public MachineKind MachineKind { get; set; } = MachineKind.Pc;
    public int SyncIntervalSeconds { get; set; } = 10;
    public bool EnableDestructiveCommands { get; set; }
}
