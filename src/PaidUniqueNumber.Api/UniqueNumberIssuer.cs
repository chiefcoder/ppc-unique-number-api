namespace PaidUniqueNumber.Api;

public sealed class UniqueNumberIssuer
{
    private long _next = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;

    public long Next() => Interlocked.Increment(ref _next);
}
