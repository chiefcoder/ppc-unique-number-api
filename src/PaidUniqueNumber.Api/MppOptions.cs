namespace PaidUniqueNumber.Api;

public sealed class MppOptions
{
    public const string SectionName = "Mpp";

    public string SecretKey { get; set; } = "local-dev-secret-change-me-please-32b";
    public bool AllowDemo { get; set; } = true;
    public int AmountCents { get; set; } = 50;
    public string Currency { get; set; } = "usd";
    public int Decimals { get; set; } = 2;
    public string Realm { get; set; } = "paid-unique-number-api";
    public string Description { get; set; } = "A unique number";
    public int ChallengeTtlSeconds { get; set; } = 300;
}

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2026-09-30.preview";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(ProfileId);
}
