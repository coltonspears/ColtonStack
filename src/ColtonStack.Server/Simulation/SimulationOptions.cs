namespace ColtonStack.Server.Simulation;

/// <summary>
/// Settings for the background chat-activity simulator (from <c>ColtonStack:Simulation</c>).
/// It exists so a single demo client looks like a living workspace — teammates typing and
/// posting while you watch, without a second human.
/// </summary>
public sealed class SimulationOptions
{
    public bool Enabled { get; set; } = true;

    public int MinIntervalSeconds { get; set; } = 5;

    public int MaxIntervalSeconds { get; set; } = 15;

    /// <summary>Broadcast a typing notification for the simulated author before their message lands.</summary>
    public bool SimulateTyping { get; set; } = true;
}
