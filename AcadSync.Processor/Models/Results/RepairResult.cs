namespace AcadSync.Processor.Models.Results;

/// <summary>
/// Result of a repair operation
/// </summary>
public class RepairResult
{
    /// <summary>
    /// Unique identifier for this repair run
    /// </summary>
    public string RepairId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when repair started
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when repair completed
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Duration of the repair operation
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

    /// <summary>
    /// Staff ID that performed the repair
    /// </summary>
    public int StaffId { get; init; }

    /// <summary>
    /// Total number of violations attempted to repair
    /// </summary>
    public int TotalViolationsAttempted { get; set; }

    /// <summary>
    /// Number of violations successfully repaired
    /// </summary>
    public int SuccessfulRepairs { get; set; }

    /// <summary>
    /// Number of violations that failed to repair
    /// </summary>
    public int FailedRepairs { get; set; }

    /// <summary>
    /// Details of successful repairs
    /// </summary>
    public List<RepairDetail> SuccessfulRepairDetails { get; init; } = new();

    /// <summary>
    /// Details of failed repairs
    /// </summary>
    public List<RepairFailure> FailedRepairDetails { get; init; } = new();

    /// <summary>
    /// Whether the overall repair operation completed successfully
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// General error message if repair operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception details if repair operation failed
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Success rate as a percentage (computed from successful + failed repairs to avoid relying on a separate counter)
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = SuccessfulRepairs + FailedRepairs;
            return total > 0 ? (double)SuccessfulRepairs / total * 100.0 : 0.0;
        }
    }
}

/// <summary>
/// Details of a successful repair
/// </summary>
public class RepairDetail
{
    public string RuleId { get; init; } = "";
    public string EntityType { get; init; } = "";
    public long EntityId { get; init; }
    public string PropertyCode { get; init; } = "";
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public DateTimeOffset RepairedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Action { get; init; } = "";
}

/// <summary>
/// Details of a failed repair
/// </summary>
public class RepairFailure
{
    public string RuleId { get; init; } = "";
    public string EntityType { get; init; } = "";
    public long EntityId { get; init; }
    public string PropertyCode { get; init; } = "";
    public string? AttemptedValue { get; init; }
    public string ErrorMessage { get; init; } = "";
    public Exception? Exception { get; init; }
    public DateTimeOffset FailedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Combined validation and repair result
/// </summary>
public class ValidationAndRepairResult
{
    /// <summary>
    /// Unique identifier for this operation
    /// </summary>
    public string OperationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Validation phase result
    /// </summary>
    public ValidationResult ValidationResult { get; set; } = new() { Mode = EprlMode.repair };

    /// <summary>
    /// Repair phase result (null if no repairs were attempted)
    /// </summary>
    public RepairResult? RepairResult { get; set; }

    /// <summary>
    /// Whether both validation and repair completed successfully
    /// </summary>
    public bool IsSuccess => ValidationResult.IsSuccess && (RepairResult?.IsSuccess ?? true);

    /// <summary>
    /// Combined error messages from both phases
    /// </summary>
    public string? ErrorMessage
    {
        get
        {
            var errors = new List<string>();
            if (!string.IsNullOrEmpty(ValidationResult.ErrorMessage))
                errors.Add($"Validation: {ValidationResult.ErrorMessage}");
            if (!string.IsNullOrEmpty(RepairResult?.ErrorMessage))
                errors.Add($"Repair: {RepairResult.ErrorMessage}");
            return errors.Any() ? string.Join("; ", errors) : null;
        }
    }

    /// <summary>
    /// Total duration of both validation and repair phases
    /// </summary>
    public TimeSpan TotalDuration => ValidationResult.Duration + (RepairResult?.Duration ?? TimeSpan.Zero);
}
