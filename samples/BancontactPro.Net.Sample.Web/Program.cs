using System.Text.Json.Serialization;
using BancontactPro.Helpers;
using BancontactPro.Net.Sample.Web.Components;
using BancontactPro.Net.Sample.Web.Debugging;
using BancontactPro.Webhooks;

const string CallbackPath = "/bancontact/callback";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Credentials come from user secrets or environment variables, as in the API sample: see the README.
builder.Services.AddBancontactProIntegration(builder.Configuration);
builder.Services.AddSingleton<DebugLog>();
builder.Services.AddSingleton<CallbackLog>();

// The library's two named HTTP pipelines (the names are internal to the library): every call to Bancontact and every
// JWKS fetch goes through the debug handler, so the Debug panel shows exactly what went over the wire.
builder.Services.AddTransient<DebugHandler>();
builder.Services.AddHttpClient("BancontactPro.Api").AddHttpMessageHandler<DebugHandler>();
builder.Services.AddHttpClient("BancontactPro.Jwks").AddHttpMessageHandler<DebugHandler>();

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();
app.MapStaticAssets();

// Bancontact calls this when a payment changes. Every call is written to the debug log, valid or not, with the
// Signature header and the exact body received: that is what is needed to tell why a verification failed.
app.MapPost(CallbackPath, async (HttpRequest request, BancontactCallbackVerifier verifier, CallbackLog callbacks, DebugLog debug, CancellationToken ct) =>
{
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer, ct);
    var body = buffer.ToArray();
    var signature = request.Headers["Signature"].ToString();
    var received = new List<KeyValuePair<string, string>>(request.Headers.Select(h => new KeyValuePair<string, string>(h.Key, h.Value.ToString())));
    var text = System.Text.Encoding.UTF8.GetString(body);
    var analysis = CallbackDiagnostics.Analyse(signature, body);

    try
    {
        var verified = await verifier.VerifyAndParseAsync(signature, body, request.Path, ct);
        callbacks.Add(new ReceivedCallback(DateTimeOffset.UtcNow, verified.RequestId, verified.Payload));
        debug.Add(new DebugEntry
        {
            At = DateTimeOffset.UtcNow,
            Kind = DebugKind.Callback,
            Title = $"Callback {verified.Payload.Status} for {verified.Payload.PaymentId}",
            Method = "POST",
            Url = request.Path,
            Status = 200,
            RequestHeaders = received,
            RequestBody = DebugLog.Pretty(text),
            RawRequestBody = text,
            Analysis = analysis,
            ResponseBody = $"Signature valid. Bancontact request ID: {verified.RequestId}",
        });
        return Results.Ok();
    }
    catch (InvalidCallbackException ex)
    {
        debug.Add(new DebugEntry
        {
            At = DateTimeOffset.UtcNow,
            Kind = DebugKind.Callback,
            Title = "Callback rejected",
            Method = "POST",
            Url = request.Path,
            Status = 401,
            RequestHeaders = received,
            RequestBody = DebugLog.Pretty(text),
            RawRequestBody = text,
            Analysis = analysis,
            Error = ex.Message,
        });
        return Results.Unauthorized();
    }
}).DisableAntiforgery(); // a server-to-server call: authenticated by its signature, not by an antiforgery token

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
