namespace ColtonStack.WebhookSink;

/// <summary>
/// Process-wide demo switch: when on, ~40% of webhook deliveries fail with 500.
/// The compiler-synthesized <c>field</c> is the backing store — no declared field to keep in sync.
/// </summary>
public static class Chaos
{
    public static bool Enabled
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    }
}
