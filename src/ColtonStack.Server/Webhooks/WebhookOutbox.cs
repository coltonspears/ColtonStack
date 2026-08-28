using System.Threading.Channels;

namespace ColtonStack.Server.Webhooks;

/// <summary>
/// Bounded in-memory outbox. <see cref="BoundedChannelFullMode.DropOldest"/> means a slow
/// webhook target can never block or fail message saves — delivery is best-effort by design.
/// </summary>
public sealed class WebhookOutbox : IWebhookOutbox
{
    private readonly Channel<WebhookJob> _channel = Channel.CreateBounded<WebhookJob>(
        new BoundedChannelOptions(capacity: 256)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

    public ValueTask EnqueueAsync(WebhookJob job, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    public IAsyncEnumerable<WebhookJob> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
