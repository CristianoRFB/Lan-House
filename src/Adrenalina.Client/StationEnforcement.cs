namespace Adrenalina.Client;

public interface IStationEnforcementService
{
    void Lock();
    void Unlock();
    void ApplySessionState(bool sessionActive);
    bool HealthCheck();
}

// Safe default for development and institutional machines. Production enforcement
// must be supplied by an explicitly installed, authorized Windows integration.
public sealed class SafeNoOpStationEnforcementService : IStationEnforcementService
{
    public void Lock() { }

    public void Unlock() { }

    public void ApplySessionState(bool sessionActive) { }

    public bool HealthCheck() => true;
}
