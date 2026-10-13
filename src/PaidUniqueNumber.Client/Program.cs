using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PaidUniqueNumber.Mpp;

var url = args.Length > 0 ? args[0] : "http://127.0.0.1:4242/unique-number";
var secretKey = Environment.GetEnvironmentVariable("CLIENT_MPP_SECRET_KEY")
    ?? "local-dev-secret-change-me-please-32b";
var stripeSecret = Environment.GetEnvironmentVariable("CLIENT_STRIPE_SECRET_KEY");
var stripeVersion = Environment.GetEnvironmentVariable("STRIPE_API_VERSION") ?? "2026-09-30.preview";

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

Console.WriteLine($"Requesting {url}");
using var unpaid = await http.GetAsync(url);

var challenges = WwwAuthenticate.Parse(
    unpaid.Headers.WwwAuthenticate.Select(h => $"{h.Scheme} {h.Parameter}"));
    Console.WriteLine();
    Console.WriteLine($"First response: \x1b[36m {(int)unpaid.StatusCode} {unpaid.ReasonPhrase} \x1b[0m for Challenge ID \x1b[35m {challenges.FirstOrDefault()?.Id ?? "none"} \x1b[0m with Payment Info {Base64Url.DecodeUtf8(challenges.FirstOrDefault()?.Request ?? "")}");

if (unpaid.StatusCode != (HttpStatusCode)402)
{
    Console.WriteLine();
    Console.WriteLine(await unpaid.Content.ReadAsStringAsync());
    return unpaid.IsSuccessStatusCode ? 0 : 1;
}

if (challenges.Count == 0)
{
    Console.Error.WriteLine("402 response did not include a Payment WWW-Authenticate challenge.");
    return 1;
}

foreach (var offered in challenges)
{
    Console.WriteLine();
    Console.WriteLine($"Offered method={offered.Method} payment={Base64Url.DecodeUtf8(offered.Request)} challenge ID \x1b[35m {offered.Id} \x1b[0m");
}

var credential = await CreateCredentialAsync(challenges);
if (credential is null)
{
    Console.Error.WriteLine("No supported payment method. Set STRIPE_SECRET_KEY for Stripe SPT, or use the demo method.");
    return 1;
}

using var paidRequest = new HttpRequestMessage(HttpMethod.Get, url);
paidRequest.Headers.TryAddWithoutValidation("Authorization", credential.EncodeAuthorization());
Console.WriteLine();
Console.WriteLine($"Now sending payment credential for method={credential.Challenge.Method} challenge ID \x1b[35m {credential.Challenge.Id} \x1b[0m with payload=\x1b[33m {credential.Payload} \x1b[0m ");

using var paid = await http.SendAsync(paidRequest);
var body = await paid.Content.ReadAsStringAsync();
Console.WriteLine();
Console.WriteLine($"Paid response: {(int)paid.StatusCode} {paid.ReasonPhrase}");
Console.WriteLine(body);

if (paid.Headers.TryGetValues("Payment-Receipt", out var receipts))
{
    var receipt = PaymentReceipt.TryDecode(receipts.First());
    if (receipt is not null)
    {
        Console.WriteLine();
        Console.WriteLine($"Receipt method={receipt.Method} reference=\x1b[34m {receipt.Reference} \x1b[0m status={receipt.Status} challenge ID= \x1b[35m{receipt.ExternalId}\x1b[0m");
    }
}

return paid.IsSuccessStatusCode ? 0 : 1;

async Task<PaymentCredential?> CreateCredentialAsync(IReadOnlyList<ChallengeWire> offered)
{
    var stripeChallenge = offered.FirstOrDefault(c => c.Method == "stripe");
    if (stripeChallenge is not null && !string.IsNullOrWhiteSpace(stripeSecret))
    {
        var requestJson = JsonNode.Parse(Base64Url.DecodeUtf8(stripeChallenge.Request)) as JsonObject
            ?? throw new InvalidOperationException("Stripe challenge request was not JSON.");
        var amount = requestJson["amount"]?.GetValue<string>() ?? "50";
        var currency = requestJson["currency"]?.GetValue<string>() ?? "usd";
        var spt = await CreateTestSptAsync(amount, currency);
        return new PaymentCredential
        {
            Challenge = ChallengeEcho.From(stripeChallenge),
            Payload = new JsonObject { ["spt"] = spt },
            Source = "stripe-test-client",
        };
    }

    var demoChallenge = offered.FirstOrDefault(c => c.Method == "demo");
    if (demoChallenge is not null)
    {
        return new PaymentCredential
        {
            Challenge = ChallengeEcho.From(demoChallenge),
            Payload = new JsonObject
            {
                ["type"] = "mac",
                ["mac"] = DemoPayment.Mac(secretKey, demoChallenge.Id),
            },
            Source = "demo-client",
        };
    }

    return null;
}

async Task<string> CreateTestSptAsync(string amountCents, string currency)
{
    var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString();
    using var content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["payment_method"] = "pm_card_visa",
        ["usage_limits[currency]"] = currency,
        ["usage_limits[max_amount]"] = amountCents,
        ["usage_limits[expires_at]"] = expiresAt,
    });

    using var request = new HttpRequestMessage(
        HttpMethod.Post,
        "https://api.stripe.com/v1/test_helpers/shared_payment/granted_tokens")
    {
        Content = content,
    };
    request.Headers.Authorization = new AuthenticationHeaderValue(
        "Basic",
        Convert.ToBase64String(Encoding.ASCII.GetBytes(stripeSecret + ":")));
    request.Headers.TryAddWithoutValidation("Stripe-Version", stripeVersion);
Console.WriteLine();
Console.WriteLine("Time now is " + DateTimeOffset.UtcNow);
Console.WriteLine($"Creating a stripe SPT with amount {amountCents} cents of {currency} that expires at {DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiresAt))}");
    using var response = await http.SendAsync(request);
    var json = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"Creating a test SPT failed: {json}");
    }

    using var document = JsonDocument.Parse(json);
    Console.WriteLine($"Created SPT: \x1b[33m {document.RootElement.GetProperty("id").GetString()}\x1b[0m");
    return document.RootElement.GetProperty("id").GetString()
        ?? throw new InvalidOperationException("Stripe did not return an SPT id.");
}
