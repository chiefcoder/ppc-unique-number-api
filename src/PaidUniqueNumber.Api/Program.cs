using PaidUniqueNumber.Api;

var builder = WebApplication.CreateBuilder(args);

var mppOptions = builder.Configuration.GetSection(MppOptions.SectionName).Get<MppOptions>() ?? new MppOptions();
var stripeOptions = builder.Configuration.GetSection(StripeOptions.SectionName).Get<StripeOptions>() ?? new StripeOptions();
if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("API_STRIPE_SECRET_KEY")))
{
    stripeOptions.SecretKey = Environment.GetEnvironmentVariable("API_STRIPE_SECRET_KEY")!;
}

if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("API_STRIPE_PROFILE_ID")))
{
    stripeOptions.ProfileId = Environment.GetEnvironmentVariable("API_STRIPE_PROFILE_ID")!;
}

if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("API_MPP_SECRET_KEY")))
{
    mppOptions.SecretKey = Environment.GetEnvironmentVariable("API_MPP_SECRET_KEY")!;
}

builder.Services.AddSingleton(mppOptions);
builder.Services.AddSingleton(stripeOptions);
builder.Services.AddSingleton<UniqueNumberIssuer>();
builder.Services.AddSingleton<ChallengeReplayStore>();
builder.Services.AddHttpClient<StripePaymentClient>();
builder.Services.AddSingleton<MppChargeService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapMethods("/unique-number", ["GET", "POST"], async (
    HttpRequest request,
    MppChargeService mpp,
    UniqueNumberIssuer numbers) =>
{
    Console.WriteLine();
    Console.WriteLine("Now processing request: " + request.Method + " " + request.Path);
    return await mpp.ChargeAsync(request, _ =>
        Results.Json(new
        {
            number = numbers.Next(),
            priceUsd = "0.50",
        }));
});

app.Run();
