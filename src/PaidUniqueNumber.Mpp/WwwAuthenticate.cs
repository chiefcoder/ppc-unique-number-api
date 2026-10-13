namespace PaidUniqueNumber.Mpp;

public static class WwwAuthenticate
{
    public static string Format(ChallengeWire challenge)
    {
        var parts = new List<string>
        {
            AuthParam("id", challenge.Id),
            AuthParam("realm", challenge.Realm),
            AuthParam("method", challenge.Method),
            AuthParam("intent", challenge.Intent),
            AuthParam("request", challenge.Request),
        };

        if (challenge.Expires is not null)
        {
            parts.Add(AuthParam("expires", challenge.Expires));
        }

        if (challenge.Description is not null)
        {
            parts.Add(AuthParam("description", challenge.Description));
        }

        if (challenge.Header is not null)
        {
            parts.Add(AuthParam("header", challenge.Header));
        }

        if (challenge.Opaque is not null)
        {
            parts.Add(AuthParam("opaque", challenge.Opaque));
        }

        return "Payment " + string.Join(", ", parts);
    }

    public static IReadOnlyList<ChallengeWire> Parse(IEnumerable<string> headerValues)
    {
        var challenges = new List<ChallengeWire>();
        foreach (var header in headerValues)
        {
            var trimmed = header.Trim();
            if (!trimmed.StartsWith("Payment", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parameters = ParseParameters(trimmed["Payment".Length..].TrimStart());
            if (!parameters.TryGetValue("id", out var id) ||
                !parameters.TryGetValue("realm", out var realm) ||
                !parameters.TryGetValue("method", out var method) ||
                !parameters.TryGetValue("intent", out var intent) ||
                !parameters.TryGetValue("request", out var request))
            {
                continue;
            }

            challenges.Add(new ChallengeWire
            {
                Id = id,
                Realm = realm,
                Method = method,
                Intent = intent,
                Request = request,
                Expires = Get(parameters, "expires"),
                Description = Get(parameters, "description"),
                Digest = Get(parameters, "digest"),
                Header = Get(parameters, "header"),
                Opaque = Get(parameters, "opaque"),
            });
        }

        return challenges;
    }

    private static string? Get(Dictionary<string, string> parameters, string name) =>
        parameters.TryGetValue(name, out var value) ? value : null;

    private static string AuthParam(string name, string value) =>
        $"{name}=\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static Dictionary<string, string> ParseParameters(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < input.Length)
        {
            while (i < input.Length && (input[i] is ' ' or ','))
            {
                i++;
            }

            var nameStart = i;
            while (i < input.Length && input[i] is not '=' and not ' ' and not ',')
            {
                i++;
            }

            if (i >= input.Length || input[i] != '=')
            {
                break;
            }

            var name = input[nameStart..i];
            i++;
            if (i >= input.Length || input[i] != '"')
            {
                break;
            }

            i++;
            var value = new System.Text.StringBuilder();
            while (i < input.Length)
            {
                var c = input[i++];
                if (c == '\\' && i < input.Length)
                {
                    value.Append(input[i++]);
                    continue;
                }

                if (c == '"')
                {
                    break;
                }

                value.Append(c);
            }

            result[name] = value.ToString();
        }

        return result;
    }
}
