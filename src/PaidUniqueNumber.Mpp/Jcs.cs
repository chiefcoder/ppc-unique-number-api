using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PaidUniqueNumber.Mpp;

/// <summary>
/// RFC 8785 JSON Canonicalization Scheme for the small JSON objects MPP puts on the wire.
/// </summary>
public static class Jcs
{
    public static string Canonicalize(JsonNode? node)
    {
        var builder = new StringBuilder();
        Write(builder, node);
        return builder.ToString();
    }

    public static string EncodeObject(JsonObject obj) => Canonicalize(obj);

    private static void Write(StringBuilder builder, JsonNode? node)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject obj:
                builder.Append('{');
                var firstProp = true;
                foreach (var property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (property.Value is null)
                    {
                        continue;
                    }

                    if (!firstProp)
                    {
                        builder.Append(',');
                    }

                    firstProp = false;
                    builder.Append(JsonSerializer.Serialize(property.Key));
                    builder.Append(':');
                    Write(builder, property.Value);
                }

                builder.Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    Write(builder, array[i]);
                }

                builder.Append(']');
                break;
            case JsonValue value:
                if (value.TryGetValue<bool>(out var boolean))
                {
                    builder.Append(boolean ? "true" : "false");
                }
                else if (value.TryGetValue<string>(out var text))
                {
                    builder.Append(JsonSerializer.Serialize(text));
                }
                else if (value.TryGetValue<decimal>(out var number))
                {
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(value.ToJsonString());
                }

                break;
            default:
                builder.Append(node.ToJsonString());
                break;
        }
    }
}
