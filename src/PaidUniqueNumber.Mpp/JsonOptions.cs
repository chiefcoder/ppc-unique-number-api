using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaidUniqueNumber.Mpp;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}
