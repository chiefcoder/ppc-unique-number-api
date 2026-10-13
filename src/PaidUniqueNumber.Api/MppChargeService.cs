using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using PaidUniqueNumber.Mpp;

namespace PaidUniqueNumber.Api;

public sealed class MppChargeService(
    MppOptions options,
    StripeOptions stripe,
    StripePaymentClient stripePayments,
    ChallengeReplayStore replayStore)
{
    public async Task<IResult> ChargeAsync(HttpRequest request, Func<PaymentReceipt, IResult> onPaid)
    {
        var challenges = BuildChallenges();
        var authorization = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return PaymentRequired(challenges, "payment-required", "This resource costs USD 0.50 per call.");
        }

        PaymentCredential credential;
        try
        {
            credential = PaymentCredential.Decode(authorization);
        }
        catch (Exception)
        {
            return PaymentRequired(challenges, "malformed-credential", "The Payment Credential could not be parsed.");
        }

        var wire = credential.Challenge.ToWire();
        if (!ChallengeBinder.Matches(options.SecretKey, wire))
        {
            return PaymentRequired(challenges, "invalid-challenge", "The Challenge id is not bound to these payment terms.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        if (wire.Expires is not null && DateTimeOffset.TryParse(wire.Expires, out var parsedExpires))
        {
            expiresAt = parsedExpires;
            if (parsedExpires < DateTimeOffset.UtcNow)
            {
                return PaymentRequired(challenges, "payment-expired", "The payment Challenge has expired.");
            }
        }

        if (!replayStore.TryConsume(wire.Id, expiresAt))
        {
            return PaymentRequired(challenges, "verification-failed", "This payment proof has already been used.");
        }

        try
        {
            var receipt = await VerifyAsync(credential, wire, request.HttpContext.RequestAborted);
            var result = onPaid(receipt);
            return new PaidResult(result, receipt);
        }
        catch (Exception ex)
        {
            return PaymentRequired(challenges, "verification-failed", ex.Message);
        }
    }

    private async Task<PaymentReceipt> VerifyAsync(
        PaymentCredential credential,
        ChallengeWire wire,
        CancellationToken cancellationToken)
    {
        if (wire.Method == "stripe")
        {
            if (!stripe.IsConfigured)
            {
                throw new InvalidOperationException("Stripe is not configured on this server.");
            }

            var spt = credential.Payload["spt"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(spt) || !spt.StartsWith("spt_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Stripe credentials must include an spt_ Shared Payment Token.");
            }

            var payment = await stripePayments.CreateAndConfirmAsync(
                wire.Id,
                spt,
                options.AmountCents,
                options.Currency,
                cancellationToken);

            if (payment.Replayed)
            {
                throw new InvalidOperationException("Payment has already been processed.");
            }

            if (payment.Status != "succeeded")
            {
                throw new InvalidOperationException($"Stripe PaymentIntent status: {payment.Status}");
            }

            return new PaymentReceipt
            {
                Method = "stripe",
                Reference = payment.Id,
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            };
        }

        if (wire.Method == "demo")
        {
            if (!options.AllowDemo)
            {
                throw new InvalidOperationException("The demo payment method is disabled.");
            }

            var mac = credential.Payload["mac"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(mac) ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(DemoPayment.Mac(options.SecretKey, wire.Id)),
                    Encoding.UTF8.GetBytes(mac)))
            {
                throw new InvalidOperationException("Demo payment MAC did not match.");
            }

            return new PaymentReceipt
            {
                Method = "demo",
                Reference = wire.Id,
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            };
        }

        throw new InvalidOperationException($"Unsupported payment method '{wire.Method}'.");
    }

    public IReadOnlyList<ChallengeWire> BuildChallenges()
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(options.ChallengeTtlSeconds).UtcDateTime
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var nonce = new JsonObject
        {
            ["n"] = Base64Url.Encode(RandomNumberGenerator.GetBytes(16)),
        };
        var opaque = Base64Url.EncodeUtf8(Jcs.EncodeObject(nonce));
        var challenges = new List<ChallengeWire>();

        if (stripe.IsConfigured)
        {
            challenges.Add(Bind(new ChallengeWire
            {
                Id = string.Empty,
                Realm = options.Realm,
                Method = "stripe",
                Intent = "charge",
                Request = Base64Url.EncodeUtf8(Jcs.EncodeObject(StripeChargeRequest.Create(
                    options.AmountCents.ToString(),
                    options.Currency,
                    options.Decimals,
                    options.Description,
                    stripe.ProfileId))),
                Expires = expires,
                Description = options.Description,
                Opaque = opaque,
            }));
        }

        if (options.AllowDemo)
        {
            challenges.Add(Bind(new ChallengeWire
            {
                Id = string.Empty,
                Realm = options.Realm,
                Method = "demo",
                Intent = "charge",
                Request = Base64Url.EncodeUtf8(Jcs.EncodeObject(StripeChargeRequest.CreateDemo(
                    options.AmountCents.ToString(),
                    options.Currency,
                    options.Decimals,
                    options.Description))),
                Expires = expires,
                Description = options.Description,
                Opaque = opaque,
            }));
        }

        if (challenges.Count == 0)
        {
            throw new InvalidOperationException("Configure Stripe or enable Mpp:AllowDemo.");
        }

        return challenges;
    }

    private ChallengeWire Bind(ChallengeWire challenge)
    {
        var withId = challenge with { Id = ChallengeBinder.ComputeId(options.SecretKey, challenge) };
        return withId;
    }

    private static IResult PaymentRequired(
        IReadOnlyList<ChallengeWire> challenges,
        string code,
        string detail)
    {
        return new PaymentRequiredResult(challenges, code, detail);
    }
}

internal sealed class PaymentRequiredResult(
    IReadOnlyList<ChallengeWire> challenges,
    string code,
    string detail) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        httpContext.Response.Headers.CacheControl = "no-store";
        foreach (var challenge in challenges)
        {
            httpContext.Response.Headers.Append("WWW-Authenticate", WwwAuthenticate.Format(challenge));
        }

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = $"https://paymentauth.org/problems/{code}",
            Title = "Payment Required",
            Status = StatusCodes.Status402PaymentRequired,
            Detail = detail,
        });
    }
}

internal sealed class PaidResult(IResult inner, PaymentReceipt receipt) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "private";
        httpContext.Response.Headers["Payment-Receipt"] = receipt.EncodeHeader();
        await inner.ExecuteAsync(httpContext);
    }
}
