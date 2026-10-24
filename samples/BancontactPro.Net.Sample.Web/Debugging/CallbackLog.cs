using System.Collections.Concurrent;
using BancontactPro.Models;

namespace BancontactPro.Net.Sample.Web.Debugging;

/// <summary>A verified callback as received: when it arrived, Bancontact's request ID, and the payload.</summary>
public sealed record ReceivedCallback(DateTimeOffset ReceivedAt, string RequestId, MerchantCallback Payload);

/// <summary>The most recent verified callbacks, in memory. A UI component subscribes to <see cref="Changed"/>.</summary>
public sealed class CallbackLog
{
    private const int Capacity = 100;
    private readonly ConcurrentQueue<ReceivedCallback> _callbacks = new();

    public event Action? Changed;

    public void Add(ReceivedCallback callback)
    {
        _callbacks.Enqueue(callback);
        while (_callbacks.Count > Capacity && _callbacks.TryDequeue(out _))
        {
        }

        Changed?.Invoke();
    }

    /// <summary>Newest first, optionally only those for one payment.</summary>
    public IReadOnlyList<ReceivedCallback> Recent(string? paymentId = null) =>
        _callbacks.Reverse().Where(c => paymentId is null || c.Payload.PaymentId == paymentId).ToList();
}
