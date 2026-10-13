using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PaidUniqueNumber.Api;

public sealed class StripePaymentClient(HttpClient http, StripeOptions stripe)
{
    public async Task<StripePaymentIntent> CreateAndConfirmAsync(
        string challengeId,
        string spt,
        int amountCents,
        string currency,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["amount"] = amountCents.ToString(),
            ["currency"] = currency,
            ["confirm"] = "true",
            ["automatic_payment_methods[enabled]"] = "true",
            ["automatic_payment_methods[allow_redirects]"] = "never",
            ["shared_payment_granted_token"] = spt,
            ["metadata[machine_payment]"] = "true",
            ["metadata[mpp_challenge_id]"] = challengeId,
            ["metadata[mpp_intent]"] = "charge",
            ["metadata[mpp_sdk]"] = "csharp",
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/payment_intents")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes(stripe.SecretKey + ":")));
        request.Headers.TryAddWithoutValidation("Stripe-Version", stripe.ApiVersion);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"mpp_{challengeId}_{spt}");
        Console.WriteLine();
        Console.WriteLine($"StripePaymentClient: Sending request to Stripe for challenge ID {challengeId} with SPT {spt} and amount {amountCents} {currency}");
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        System.Diagnostics.Debug.WriteLine(request.ToString());
        if (!response.IsSuccessStatusCode)
        {
            var message = document.RootElement.TryGetProperty("error", out var error) &&
                          error.TryGetProperty("message", out var errorMessage)
                ? errorMessage.GetString()
                : body;
            throw new InvalidOperationException(message ?? "Stripe PaymentIntent failed.");
        }

        var replayed = response.Headers.TryGetValues("Idempotent-Replayed", out var values) &&
                       values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));

        return new StripePaymentIntent(
            document.RootElement.GetProperty("id").GetString() ?? string.Empty,
            document.RootElement.GetProperty("status").GetString() ?? string.Empty,
            replayed);
    }
}

public sealed record StripePaymentIntent(string Id, string Status, bool Replayed);
