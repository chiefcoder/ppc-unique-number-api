using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaidUniqueNumber.Mpp;

public sealed record PaymentReceipt
{
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("reference")]
    public required string Reference { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "success";

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("externalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExternalId { get; init; }

    public string EncodeHeader() =>
        Base64Url.EncodeUtf8(JsonSerializer.Serialize(this, JsonOptions.Wire));

    public static PaymentReceipt? TryDecode(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PaymentReceipt>(
            Base64Url.DecodeUtf8(headerValue),
            JsonOptions.Wire);
    }
}
