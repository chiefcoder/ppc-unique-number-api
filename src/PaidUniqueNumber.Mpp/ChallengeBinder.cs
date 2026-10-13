using System.Security.Cryptography;
using System.Text;

namespace PaidUniqueNumber.Mpp;

public static class ChallengeBinder
{
    public static string ComputeId(string secretKey, ChallengeWire challenge)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(challenge.BindingMaterial()));
        return Base64Url.Encode(hash.AsSpan(0, 16));
    }

    public static bool Matches(string secretKey, ChallengeWire challenge) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(ComputeId(secretKey, challenge)),
            Encoding.UTF8.GetBytes(challenge.Id));
}
