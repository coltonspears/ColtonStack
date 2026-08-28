namespace ColtonStack.WebhookSink;

/// <summary>Process-wide demo switch: when on, ~40% of webhook deliveries fail with 500.</summary>
public static class Chaos
{
    private static int _enabled;

    public static bool Enabled
    {
        get => Volatile.Read(ref _enabled) == 1;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }
}
