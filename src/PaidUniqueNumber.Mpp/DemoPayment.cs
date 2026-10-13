using System.Security.Cryptography;
using System.Text;

namespace PaidUniqueNumber.Mpp;

public static class DemoPayment
{
    public static string Mac(string secretKey, string challengeId)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        return Base64Url.Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(challengeId)));
    }
}
