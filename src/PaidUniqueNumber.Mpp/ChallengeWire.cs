namespace PaidUniqueNumber.Mpp;

/// <summary>
/// MPP Payment challenge as it appears on the HTTP wire (request/opaque already encoded).
/// </summary>
public sealed record ChallengeWire
{
    public required string Id { get; init; }
    public required string Realm { get; init; }
    public required string Method { get; init; }
    public required string Intent { get; init; }
    public required string Request { get; init; }
    public string? Expires { get; init; }
    public string? Description { get; init; }
    public string? Digest { get; init; }
    public string? Header { get; init; }
    public string? Opaque { get; init; }

    public string BindingMaterial()
    {
        var parts = new List<string>
        {
            Realm,
            Method,
            Intent,
            Request,
            Expires ?? string.Empty,
            Digest ?? string.Empty,
        };

        if (Header is not null)
        {
            parts.Add(Header);
        }

        parts.Add(Opaque ?? string.Empty);
        return string.Join('|', parts);
    }
}
