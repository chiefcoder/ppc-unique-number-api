# Pay-per-call unique-number API (MPP)

ASP.NET Core API that returns a unique number after a **$0.50** Machine Payments Protocol (MPP) charge, plus a C# client that pays and retries.

There is no official C# MPP SDK, so this implements the HTTP `402` Challenge → Credential → Receipt flow from [mpp.dev](https://mpp.dev/protocol). Card charges use Stripe Shared Payment Tokens (minimum $0.50). A local `demo` method is on by default so you can run without Stripe keys.

## How a paid API call works

```mermaid
sequenceDiagram
	autonumber
	participant C as Client
	participant A as API
	participant P as Payment provider
	C->>A: Request resource
	A-->>C: 402 Payment Required + MPP challenge
	Note over C,A: Challenge includes the payment method and terms
	C->>P: Create payment credential (for example, Stripe SPT)
	P-->>C: Payment credential
	C->>A: Retry request + MPP Authorization credential
	A->>A: Verify challenge and credential
	A->>P: Confirm charge (Stripe method)
	P-->>A: Payment confirmed
	A-->>C: 200 Resource + Payment-Receipt
```

## Server design

```mermaid
flowchart LR
	caller[API caller] -->|HTTP request or paid retry| endpoint["/unique-number endpoint"]
	endpoint --> service[MppChargeService]
	service <--> mpp["PaidUniqueNumber.Mpp<br/>challenge wire and binding<br/>credential parsing and receipt encoding<br/>demo MAC verification"]
	service --> replay[ChallengeReplayStore]
	service -->|Stripe payment| stripeClient[StripePaymentClient]
	stripeClient <--> stripe[Stripe API]
	service --> issuer[UniqueNumberIssuer]
	endpoint -->|402 challenge or paid result + receipt| caller
```

## Client design

```mermaid
flowchart LR
	api[PaidUniqueNumber API] -->|402 challenge| client["Client Program<br/>orchestrates request, retry, and output"]
	client <--> mpp["PaidUniqueNumber.Mpp<br/>parse challenge<br/>build and encode credential<br/>decode receipt"]
	client -->|optional test SPT request| stripe[Stripe test API]
	stripe -->|SPT| client
	client -->|paid retry with Authorization| api
	api -->|resource + Payment-Receipt| client
```

## Run locally (demo payment)

Terminal 1:

```bash
dotnet run --project src/PaidUniqueNumber.Api
```

Terminal 2:

```bash
dotnet run --project src/PaidUniqueNumber.Client
```

The client requests `GET /unique-number`, receives `402 Payment Required`, attaches a demo Credential, and prints the unique number plus a `Payment-Receipt`.

## Stripe SPT ($0.50 USD)

1. Create a Stripe sandbox API key and [business profile](https://docs.stripe.com/payments/machine/mpp).
2. Set the same values on the API and the client:

```bash
export API_STRIPE_SECRET_KEY=sk_test_...
export API_STRIPE_PROFILE_ID=profile_test_...
export CLIENT_STRIPE_SECRET_KEY=sk_test_...
export API_MPP_SECRET_KEY='a long random secret'
export CLIENT_MPP_SECRET_KEY='a long random secret'
```

3. Restart the API. It now advertises `method="stripe"` for 50 USD cents.
4. Run the client with `CLIENT_STRIPE_SECRET_KEY` set. It creates a test Shared Payment Token (`pm_card_visa`) and retries with that Credential. The API confirms a Stripe PaymentIntent using `API_STRIPE_SECRET_KEY` and then returns the number.
5. For the demo method, keep the same secret on both sides or set `CLIENT_MPP_SECRET_KEY` to match `API_MPP_SECRET_KEY` so the MAC challenge verifies correctly.

Keep `Mpp:AllowDemo` true in `appsettings.json` only for local development.
