using System.Collections.Concurrent;

namespace Adrenalina.Server.Infrastructure;

public sealed class MachineReplayGuard
{
    private readonly ConcurrentDictionary<string, DateTime> _seen = new(StringComparer.Ordinal);

    public bool TryAccept(string nonce)
    {
        var now = DateTime.UtcNow;
        foreach (var item in _seen)
        {
            if (item.Value < now.AddMinutes(-3))
            {
                _seen.TryRemove(item.Key, out _);
            }
        }

        return _seen.TryAdd(nonce, now);
    }
}
