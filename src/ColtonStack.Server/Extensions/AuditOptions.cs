namespace ColtonStack.Server.Extensions;

/// <summary>Bound from <c>ColtonStack:Audit</c>. Extension-owned configuration — the core never reads these.</summary>
public sealed class AuditOptions
{
    public const string SectionName = "ColtonStack:Audit";

    public int DefaultPageSize { get; set; } = 50;

    public int MaxPageSize { get; set; } = 500;
}
