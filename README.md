# Pay-per-use unique-number API (using MPP)

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

## System design

### Server

```mermaid
flowchart TD
	Caller[HTTP client] --> Endpoint[ASP.NET Core routes<br/>/health and /unique-number]
	Config[MppOptions and StripeOptions] --> Charge
	Endpoint --> Charge[MppChargeService<br/>payment orchestration]

	Charge -->|No Authorization| Build[BuildChallenges]
	Build --> Bind[ChallengeBinder<br/>HMAC challenge ID]
	Build --> RequestShape[StripeChargeRequest + Jcs + Base64Url]
	Bind --> Challenge[ChallengeWire]
	RequestShape --> Challenge
	Challenge --> Format[WwwAuthenticate.Format]
	Format --> Required[PaymentRequiredResult<br/>HTTP 402 + WWW-Authenticate]
	Required --> Caller

	Charge -->|Authorization present| Decode[PaymentCredential.Decode]
	Decode --> Echo[ChallengeEcho.ToWire]
	Echo --> VerifyId[ChallengeBinder.Matches]
	VerifyId --> Expiry[Expiry check]
	Expiry --> Replay[ChallengeReplayStore.TryConsume]
	Replay --> Method{Payment method}

	Method -->|stripe| StripeAdapter[StripePaymentClient]
	StripeAdapter --> Stripe[Stripe API<br/>PaymentIntent]
	Stripe -->|Succeeded| Receipt[PaymentReceipt]
	Method -->|demo| DemoMac[DemoPayment.Mac<br/>compute expected MAC]
	DemoMac --> VerifyDemo[MppChargeService<br/>fixed-time MAC comparison]
	VerifyDemo -->|Valid| Receipt

	Receipt --> Issue[UniqueNumberIssuer.Next]
	Issue --> Result[JSON resource response]
	Result --> Paid[PaidResult<br/>Payment-Receipt header]
	Paid --> Caller
	VerifyId -. invalid .-> Required
	Expiry -. expired .-> Required
	Replay -. replay .-> Required
	Stripe -. failed .-> Required
	VerifyDemo -. invalid .-> Required

	subgraph MPP[PaidUniqueNumber.Mpp]
		Challenge
		Bind
		RequestShape
		Format
		Decode
		Echo
		VerifyId
		DemoMac
		Receipt
	end
```

### Client

```mermaid
flowchart TD
	Args[CLI URL argument] --> Program[Client Program.cs]
	Env[Environment configuration<br/>MPP secret, Stripe secret, API version] --> Program
	Program --> Initial[HttpClient: initial resource request]
	Initial --> API[PaidUniqueNumber API]
	API -->|200| Done[Print response]
	API -->|402 + WWW-Authenticate| Parse[WwwAuthenticate.Parse]
	Parse --> Offers[ChallengeWire offers]
	Offers --> Select{Supported method<br/>and credentials configured?}

	Select -->|stripe| StripeSpt[CreateTestSptAsync]
	StripeSpt --> StripeTest[Stripe test helpers<br/>create Shared Payment Token]
	StripeTest --> Spt[SPT]
	Select -->|demo| Mac[DemoPayment.Mac<br/>challenge ID + MPP secret]
	Spt --> StripeCredential[PaymentCredential<br/>ChallengeEcho.From + SPT payload]
	Mac --> DemoCredential[PaymentCredential<br/>ChallengeEcho.From + MAC payload]
	StripeCredential --> Encode[EncodeAuthorization]
	DemoCredential --> Encode
	Encode --> Retry[HttpClient: retry with Authorization]
	Retry --> API
	API -->|Resource + Payment-Receipt| Receipt[PaymentReceipt.TryDecode]
	Receipt --> Done
	Select -->|No match| Error[Report unsupported method]

	subgraph MPP[PaidUniqueNumber.Mpp]
		Parse
		Offers
		StripeCredential
		DemoCredential
		Mac
		Encode
		Receipt
	end
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
