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

/// <summary>
/// Audit statistics for reporting
/// </summary>
public sealed record AuditStatistics(
    int TotalViolations,
    int TotalRepairs,
    int ValidationRuns,
    Dictionary<string, int> ViolationsByRule,
    Dictionary<string, int> ViolationsBySeverity,
    DateTime? LastRunDate
);
