using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PaidUniqueNumber.Mpp;

public sealed record PaymentCredential
{
    [JsonPropertyName("challenge")]
    public required ChallengeEcho Challenge { get; init; }

    [JsonPropertyName("payload")]
    public required JsonObject Payload { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    public static PaymentCredential Decode(string authorizationValue)
    {
        var token = authorizationValue.Trim();
        if (token.StartsWith("Payment ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Payment ".Length..].Trim();
        }

        var json = Base64Url.DecodeUtf8(token);
        return JsonSerializer.Deserialize<PaymentCredential>(json, JsonOptions.Wire)
            ?? throw new InvalidOperationException("Credential JSON was empty.");
    }

    public string EncodeAuthorization() =>
        "Payment " + Base64Url.EncodeUtf8(JsonSerializer.Serialize(this, JsonOptions.Wire));
}

public sealed record ChallengeEcho
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("realm")]
    public required string Realm { get; init; }

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("intent")]
    public required string Intent { get; init; }

    [JsonPropertyName("request")]
    public required string Request { get; init; }

    [JsonPropertyName("expires")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expires { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("digest")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Digest { get; init; }

    [JsonPropertyName("header")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Header { get; init; }

    [JsonPropertyName("opaque")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Opaque { get; init; }

    public static ChallengeEcho From(ChallengeWire challenge) => new()
    {
        Id = challenge.Id,
        Realm = challenge.Realm,
        Method = challenge.Method,
        Intent = challenge.Intent,
        Request = challenge.Request,
        Expires = challenge.Expires,
        Description = challenge.Description,
        Digest = challenge.Digest,
        Header = challenge.Header,
        Opaque = challenge.Opaque,
    };

    public ChallengeWire ToWire() => new()
    {
        Id = Id,
        Realm = Realm,
        Method = Method,
        Intent = Intent,
        Request = Request,
        Expires = Expires,
        Description = Description,
        Digest = Digest,
        Header = Header,
        Opaque = Opaque,
    };
}
