using System.Text.Json.Nodes;

namespace PaidUniqueNumber.Mpp;

public static class StripeChargeRequest
{
    public static JsonObject Create(
        string amountCents,
        string currency,
        int decimals,
        string description,
        string networkId)
    {
        return new JsonObject
        {
            ["amount"] = amountCents,
            ["currency"] = currency,
            ["decimals"] = decimals,
            ["description"] = description,
            ["methodDetails"] = new JsonObject
            {
                ["networkId"] = networkId,
                ["paymentMethodTypes"] = new JsonArray(JsonValue.Create("card"), JsonValue.Create("link")),
            },
        };
    }

    public static JsonObject CreateDemo(string amountCents, string currency, int decimals, string description)
    {
        return new JsonObject
        {
            ["amount"] = amountCents,
            ["currency"] = currency,
            ["decimals"] = decimals,
            ["description"] = description,
        };
    }
}
