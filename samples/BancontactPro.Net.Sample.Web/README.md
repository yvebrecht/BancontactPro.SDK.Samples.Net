# BancontactPro.Net sample web UI

A small Blazor Server app over the `BancontactPro.Net` library, to try a payment by hand against Bancontact's preprod with the
Bancontact Pay test app, and to see what goes over the wire while you do.

It calls the library directly (no sample API in between) and is meant to run **locally**: it listens on `localhost` only and
has no login, because it calls Bancontact with your credentials.

## Pages

| Page | What it does |
|---|---|
| Status | Environment, which products have credentials, the callback URL. |
| Betaling | Creates a payment (or a Static QR payment with a POS ID), shows the QR code and deeplink, follows the status every 2 s and from callbacks, cancels. The debug panel below follows that payment. |
| QR-test | Builds the self-built payload URLs (`/t/1/{PPID}` and `/l/1/{PPID}/{POSId}`) and the generator image, without an API call. Scan with the preprod test app. |
| Callbacks | The verified callbacks Bancontact sent to `POST /bancontact/callback`. |
| Debug | Everything below, unfiltered. |

## Debug information

Every call the library makes to Bancontact and every callback goes into the debug log:

- request: method, URL, headers (the `Authorization` API key is masked to its last 4 characters; the `Signature` header is shown), body;
- response: status, headers, body, duration; for errors the `traceId` and `spanId` that Bancontact support asks for;
- callback: all received headers (including `Signature`), the exact body, and the verification result. A rejected callback shows the reason.
- JWKS fetches of the callback verifier appear as requests too.

This works through a `DelegatingHandler` added to the library's two named HTTP pipelines (`BancontactPro.Api` and `BancontactPro.Jwks`,
see `Program.cs`). The library's credential and signing handlers wrap that pipeline, so the log shows the request as it was sent.

## Run

```bash
cd samples/BancontactPro.Net.Sample.Web
dotnet user-secrets set "BancontactPro:Products:OnDisplay:ApiKey" "<API key from the onboarding email>"
dotnet user-secrets set "BancontactPro:Products:OnDisplay:MerchantProfileId" "<PPID from the email>"
dotnet run
```

Open http://localhost:5084. Set the keys of every product you want to try (`OnDisplay`, `OnReceipt`, `StaticQr`, `TopUp`, `Invoice`).
The environment is `Preprod` unless `BancontactPro:Environment` says `Production`: do not point this app at production with real money.

## Docker

The library is not on NuGet yet and the project references the sister folder `BancontactPro.Net`, so the image is built from the folder that holds both repositories (`docker-compose.yml` sets that context).

```bash
cp .env.example .env   # fill in the credentials
docker compose up --build
```

Open http://localhost:5084. The port is published on `127.0.0.1` only, as the app has no login. `.env` also sets `PUBLIC_BASE_URL` and `SAMPLE_WEB_PORT`.

## Callbacks

Bancontact must reach `POST /bancontact/callback` over public HTTPS. Start a tunnel to port 5084, then set
`PublicBaseUrl` (user secret or environment variable) to the tunnel's address, without trailing slash, and restart.
The Betaling page sends `PublicBaseUrl` + `/bancontact/callback` as the payment's `callbackUrl`. Without it the callback URL is
`http://localhost:5084/...`, which Bancontact cannot reach: the payment still works, you just follow it by polling.
The signature covers the request path, so the tunnel must not rewrite it.
