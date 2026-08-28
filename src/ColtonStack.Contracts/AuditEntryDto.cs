namespace ColtonStack.Contracts;

/// <summary>
/// One audit trail entry. Written by <c>IAuditService</c> — a plain service that receives
/// dumb records — in contrast to legacy models that carried auditing inside their base class.
/// </summary>
public sealed record AuditEntryDto(
    long Id,
    string EntityType,
    long EntityId,
    string Action,
    string Actor,
    DateTimeOffset TimestampUtc,
    string? PayloadJson);
