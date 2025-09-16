namespace AcadSync.Audit.Models;

/// <summary>
/// Represents an audit entry for extended property changes
/// </summary>
public sealed record AuditEntry(
    string RuleId,
    string EntityType,
    long EntityId,
    string PropertyCode,
    string? CurrentValue,
    string? ProposedValue,
    string Action,
    string Severity
);
