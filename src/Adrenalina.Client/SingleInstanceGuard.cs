namespace Adrenalina.Client;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    private SingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static SingleInstanceGuard? TryAcquire(string name)
    {
        var mutex = new Mutex(true, name, out var createdNew);
        return createdNew ? new SingleInstanceGuard(mutex) : null;
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
