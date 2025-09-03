namespace AcadSync.Audit.Models;

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
