namespace ColtonStack.Server.Infrastructure;

/// <summary>
/// Lets an extension own its tables. The initializer runs the core schema, then every
/// registered contributor's DDL, all idempotent (<c>CREATE TABLE IF NOT EXISTS</c>). An
/// extension that needs storage registers one of these — it never edits the core schema string.
/// </summary>
public interface ISchemaContributor
{
    /// <summary>Human-readable name for the startup log.</summary>
    string Name { get; }

    /// <summary>Idempotent DDL, executed once at startup after the core tables exist.</summary>
    string Schema { get; }
}
