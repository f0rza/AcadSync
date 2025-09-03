namespace AcadSync.Processor;

/// <summary>
/// Repository interface for audit operations against AcadSync audit database
/// </summary>
public interface IAuditRepository
{
    /// <summary>
    /// Write audit record for a violation/repair
    /// </summary>
    Task WriteAuditAsync(Violation violation, int staffId, string? notes = null);

    /// <summary>
    /// Start a new validation run and return the run ID
    /// </summary>
    Task<long> StartValidationRunAsync(string mode, int staffId, string? notes = null);

    /// <summary>
    /// Complete a validation run with summary statistics
    /// </summary>
    Task CompleteValidationRunAsync(long runId, int violationCount, int repairedCount, string? notes = null);

    /// <summary>
    /// Log system events and errors
    /// </summary>
    Task LogSystemEventAsync(string level, string message, string? exception = null, string? source = null);

    /// <summary>
    /// Test audit database connection
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Get audit statistics for reporting
    /// </summary>
    Task<AuditStatistics> GetAuditStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Clean up old audit records based on retention policy
    /// </summary>
    Task CleanupOldAuditRecordsAsync(int retentionDays);
}

/// <summary>
/// Audit statistics for reporting
/// </summary>
public sealed record AuditStatistics(
    int TotalViolations,
    int TotalRepairs,
    int ValidationRuns,
    Dictionary<string, int> ViolationsByRule,
    Dictionary<Severity, int> ViolationsBySeverity,
    DateTime? LastRunDate
);
