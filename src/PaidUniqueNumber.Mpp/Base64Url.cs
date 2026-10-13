namespace PaidUniqueNumber.Mpp;

public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string EncodeUtf8(string value) =>
        Encode(System.Text.Encoding.UTF8.GetBytes(value));

    public static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return (padded.Length % 4) switch
        {
            2 => Convert.FromBase64String(padded + "=="),
            3 => Convert.FromBase64String(padded + "="),
            0 => Convert.FromBase64String(padded),
            _ => throw new FormatException("Invalid base64url value."),
        };
    }

    public static string DecodeUtf8(string value) =>
        System.Text.Encoding.UTF8.GetString(Decode(value));
}
