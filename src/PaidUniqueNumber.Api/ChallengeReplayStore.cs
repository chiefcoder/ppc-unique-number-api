using System.Collections.Concurrent;

namespace PaidUniqueNumber.Api;

public sealed class ChallengeReplayStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _used = new(StringComparer.Ordinal);

    public bool TryConsume(string challengeId, DateTimeOffset expiresAt)
    {
        Prune();
        return _used.TryAdd(challengeId, expiresAt);
    }

    private void Prune()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _used)
        {
            if (pair.Value < now)
            {
                _used.TryRemove(pair.Key, out _);
            }
        }
    }
}
