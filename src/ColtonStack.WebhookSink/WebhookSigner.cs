using System.Security.Cryptography;

namespace ColtonStack.WebhookSink;

/// <summary>Mirrors the server's signing contract so the sink can verify deliveries.</summary>
public static class WebhookSigner
{
    public const string HeaderName = "X-ColtonStack-Signature";
}
