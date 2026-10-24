using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BancontactPro.Net.Sample.Web.Debugging;

/// <summary>
/// Records every request the library sends to Bancontact and the response it gets. The library's credential and signing
/// handlers wrap this pipeline, so the request is seen as it goes out on the wire: with its Authorization and Signature headers.
/// The API key itself is masked; everything else is shown as it is.
/// </summary>
public sealed class DebugHandler(DebugLog log) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var requestHeaders = Flatten(request.Headers, request.Content?.Headers);
        var stopwatch = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Add(new DebugEntry
            {
                At = DateTimeOffset.UtcNow,
                Kind = DebugKind.Request,
                Title = $"{request.Method} {request.RequestUri?.AbsolutePath}",
                Method = request.Method.Method,
                Url = request.RequestUri?.ToString(),
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                RequestHeaders = requestHeaders,
                RequestBody = DebugLog.Pretty(requestBody),
                Error = $"{ex.GetType().Name}: {ex.Message}",
            });
            throw;
        }

        string? responseBody = null;
        if (response.Content is not null)
        {
            // Buffer it, so the library can still read the body after we did.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        var (traceId, spanId) = response.IsSuccessStatusCode ? (null, null) : ReadTraceIds(responseBody);
        log.Add(new DebugEntry
        {
            At = DateTimeOffset.UtcNow,
            Kind = DebugKind.Request,
            Title = $"{request.Method} {request.RequestUri?.AbsolutePath}",
            Method = request.Method.Method,
            Url = request.RequestUri?.ToString(),
            Status = (int)response.StatusCode,
            ElapsedMs = stopwatch.ElapsedMilliseconds,
            RequestHeaders = requestHeaders,
            RequestBody = DebugLog.Pretty(requestBody),
            ResponseHeaders = Flatten(response.Headers, response.Content?.Headers),
            ResponseBody = DebugLog.Pretty(responseBody),
            TraceId = traceId,
            SpanId = spanId,
        });

        return response;
    }

    private static List<KeyValuePair<string, string>> Flatten(HttpHeaders headers, HttpHeaders? contentHeaders)
    {
        var result = new List<KeyValuePair<string, string>>();
        foreach (var header in headers.Concat(contentHeaders ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()))
        {
            var value = string.Join(", ", header.Value);
            result.Add(new(header.Key, header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ? MaskAuthorization(value) : value));
        }

        return result;
    }

    /// <summary>"Bearer abcdef1234" becomes "Bearer ••••1234": enough to tell which key was used, not enough to reuse it.</summary>
    private static string MaskAuthorization(string value)
    {
        var parts = value.Split(' ', 2);
        var token = parts[^1];
        var masked = token.Length > 8 ? "••••" + token[^4..] : "••••";
        return parts.Length == 2 ? $"{parts[0]} {masked}" : masked;
    }

    private static (string? TraceId, string? SpanId) ReadTraceIds(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            string? Read(string name) => document.RootElement.EnumerateObject()
                .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

            return (Read("traceId"), Read("spanId"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
