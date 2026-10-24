using System.Collections.Concurrent;
using System.Text.Json;

namespace BancontactPro.Net.Sample.Web.Debugging;

public enum DebugKind
{
    Request,
    Callback,
    Info,
}

/// <summary>One thing worth showing while debugging a payment: an HTTP exchange with Bancontact, a received callback, or a note.</summary>
public sealed record DebugEntry
{
    public required DateTimeOffset At { get; init; }

    public required DebugKind Kind { get; init; }

    public required string Title { get; init; }

    public string? Method { get; init; }

    public string? Url { get; init; }

    public int? Status { get; init; }

    public long? ElapsedMs { get; init; }

    public IReadOnlyList<KeyValuePair<string, string>> RequestHeaders { get; init; } = [];

    public string? RequestBody { get; init; }

    /// <summary>The body exactly as received (not pretty-printed): the bytes a callback signature covers.</summary>
    public string? RawRequestBody { get; init; }

    /// <summary>What the sample worked out about a callback's Signature header, to show why a verification failed.</summary>
    public string? Analysis { get; init; }

    public IReadOnlyList<KeyValuePair<string, string>> ResponseHeaders { get; init; } = [];

    public string? ResponseBody { get; init; }

    public string? TraceId { get; init; }

    public string? SpanId { get; init; }

    public string? Error { get; init; }

    public bool IsFailure => Error is not null || Status is >= 400;
}

/// <summary>The most recent debug entries, in memory. A UI component subscribes to <see cref="Changed"/> to refresh itself.</summary>
public sealed class DebugLog
{
    private const int Capacity = 300;
    private readonly ConcurrentQueue<DebugEntry> _entries = new();

    public event Action? Changed;

    public void Add(DebugEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > Capacity && _entries.TryDequeue(out _))
        {
        }

        Changed?.Invoke();
    }

    public void Info(string title, string? body = null) =>
        Add(new DebugEntry { At = DateTimeOffset.UtcNow, Kind = DebugKind.Info, Title = title, ResponseBody = body });

    /// <summary>Newest first.</summary>
    public IReadOnlyList<DebugEntry> Recent() => _entries.Reverse().ToList();

    public void Clear()
    {
        _entries.Clear();
        Changed?.Invoke();
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Pretty-prints JSON for display; anything else is returned as it is.</summary>
    public static string? Pretty(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        try
        {
            return JsonSerializer.Serialize(JsonDocument.Parse(body).RootElement, Indented);
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
