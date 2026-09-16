using System.IO;
using Adrenalina.Application;

namespace Adrenalina.Client;

public interface IStationEnforcementService
{
    void Lock();
    void Unlock();
    void ApplySessionState(bool sessionActive);
    void EnterMaintenance();
    void ExitMaintenance();
    void Restart();
    void Shutdown();
    void Logoff();
    bool HealthCheck();
}

// Safe default for development and institutional machines. Production enforcement
// must be supplied by an explicitly installed, authorized Windows integration.
public sealed class SafeNoOpStationEnforcementService : IStationEnforcementService
{
    public void Lock() { }

    public void Unlock() { }

    public void ApplySessionState(bool sessionActive) { }
    public void EnterMaintenance() { }
    public void ExitMaintenance() { }
    public void Restart() { }
    public void Shutdown() { }
    public void Logoff() { }

    public bool HealthCheck() => true;
}

public sealed class WindowsAgentStationEnforcementService(
    ClientConnectionOptions options,
    ClientCredentialStore credentialStore) : IStationEnforcementService
{
    private bool _healthy;

    public void Lock() => Send("lock", sessionActive: false);
    public void Unlock() => Send("unlock", sessionActive: true);
    public void ApplySessionState(bool sessionActive) => Send(sessionActive ? "unlock" : "lock", sessionActive);
    public void EnterMaintenance() => Send("enter-maintenance", sessionActive: true);
    public void ExitMaintenance() => Send("exit-maintenance", sessionActive: false);
    public void Restart() => Send("restart", sessionActive: false);
    public void Shutdown() => Send("shutdown", sessionActive: false);
    public void Logoff() => Send("logoff", sessionActive: false);
    public bool HealthCheck() => _healthy;

    private void Send(string action, bool sessionActive)
    {
        try
        {
            var secret = credentialStore.Load(options.MachineCredentialId) ?? options.MachineKey;
            var issuedAt = DateTime.UtcNow;
            var nonce = Guid.NewGuid().ToString("N");
            var proof = string.IsNullOrWhiteSpace(secret)
                ? string.Empty
                : Adrenalina.Application.MachineAuthentication.CreateProof(secret, issuedAt, nonce, action);
            using var client = new System.IO.Pipes.NamedPipeClientStream(".", StationAgentProtocol.PipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
            client.Connect(250);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            using var reader = new StreamReader(client);
            writer.WriteLine(System.Text.Json.JsonSerializer.Serialize(new StationAgentRequest
            {
                Action = action,
                MachineId = options.MachineId,
                SessionActive = sessionActive,
                IssuedAtUtc = issuedAt,
                Nonce = nonce,
                Proof = proof
            }, Adrenalina.Application.JsonDefaults.Options));
            var response = reader.ReadLine();
            var parsed = response is null ? null : System.Text.Json.JsonSerializer.Deserialize<StationAgentResponse>(response, Adrenalina.Application.JsonDefaults.Options);
            _healthy = parsed?.Healthy == true;
        }
        catch (Exception)
        {
            _healthy = false;
        }
    }

    private static string AgentSecretPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Adrenalina", "Agent", "machine-secret.dpapi");
}
