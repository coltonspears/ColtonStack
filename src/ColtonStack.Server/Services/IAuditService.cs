using System.Text.Json.Serialization.Metadata;
using ColtonStack.Contracts;

namespace ColtonStack.Server.Services;

/// <summary>
/// Auditing as a *service*, not a base-class feature.
///
/// Legacy models carried auditing inside themselves: set <c>EnableAuditing = true</c> and the
/// base class magically stamped audit columns and trail rows on every save — untestable,
/// unavoidable, and inherited by every model whether it wanted it or not.
///
/// Here the model stays a dumb record; callers hand it to this service alongside the JSON
/// metadata used to serialize the payload (source-generated — no reflection), and auditing
/// is just another dependency you compose where you need it.
/// </summary>
public interface IAuditService
{
    /// <summary>Writes one audit entry. The payload is serialized with caller-provided source-generated metadata.</summary>
    Task RecordAsync<TEntity>(
        string entityType,
        long entityId,
        string action,
        string actor,
        TEntity entity,
        JsonTypeInfo<TEntity> entityJsonTypeInfo,
        CancellationToken cancellationToken);

    /// <summary>Returns the most recent audit entries, newest first.</summary>
    Task<IReadOnlyList<AuditEntryDto>> GetRecentAsync(int limit, CancellationToken cancellationToken);
}
