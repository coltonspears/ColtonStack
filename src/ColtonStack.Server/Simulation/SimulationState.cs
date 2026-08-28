namespace ColtonStack.Server.Simulation;

/// <summary>Runtime switch for the simulator — defaults from <see cref="SimulationOptions"/>, toggled via <c>POST /api/simulation/&#123;enabled&#125;</c>.</summary>
public sealed class SimulationState
{
    public bool Enabled { get; set; }
}
