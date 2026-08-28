using System.Threading.Channels;

namespace ColtonStack.Server.Webhooks;

/// <summary>
/// Decouples "a message was saved" from "webhooks were delivered": saves enqueue and return
/// immediately; a background service drains the outbox at its own pace with retries.
/// </summary>
public interface IWebhookOutbox
{
    ValueTask EnqueueAsync(WebhookJob job, CancellationToken cancellationToken);

    IAsyncEnumerable<WebhookJob> DequeueAllAsync(CancellationToken cancellationToken);
}
