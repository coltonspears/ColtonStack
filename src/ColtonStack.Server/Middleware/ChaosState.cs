namespace ColtonStack.Server.Middleware;

/// <summary>
/// Runtime switch for the chaos middleware — an injected singleton like <c>SimulationState</c>,
/// not a static. Whoever needs it asks the container; tests construct one and pass it in.
/// The compiler-synthesized <c>field</c> is the backing store; the accessors add the volatile
/// semantics a cross-request flag needs.
/// </summary>
public sealed class ChaosState
{
    public bool Enabled
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    }
}
