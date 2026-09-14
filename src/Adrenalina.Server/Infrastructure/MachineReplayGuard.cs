using System.Collections.Concurrent;

namespace Adrenalina.Server.Infrastructure;

public sealed class MachineReplayGuard
{
    private const int DefaultMaximumEntries = 100_000;
    private readonly ConcurrentDictionary<string, DateTime> _seen = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly int _maximumEntries;

    public MachineReplayGuard(int maximumEntries = DefaultMaximumEntries)
    {
        if (maximumEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        }

        _maximumEntries = maximumEntries;
    }

    public bool TryAccept(string nonce)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            foreach (var item in _seen)
            {
                if (item.Value < now.AddMinutes(-3))
                {
                    _seen.TryRemove(item.Key, out _);
                }
            }

            if (_seen.Count >= _maximumEntries && !_seen.ContainsKey(nonce))
            {
                return false;
            }

            return _seen.TryAdd(nonce, now);
        }
    }
}
