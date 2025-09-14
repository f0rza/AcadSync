namespace AcadSync.Processor.Models.Results;

/// <summary>
/// Result of a validation operation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Unique identifier for this validation run
    /// </summary>
    public string ValidationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when validation started
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when validation completed
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Duration of the validation operation
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

    /// <summary>
    /// Validation mode used
    /// </summary>
    public EprlMode Mode { get; init; }

    /// <summary>
    /// Ruleset information
    /// </summary>
    public RuleSetInfo RuleSet { get; set; } = new();

    /// <summary>
    /// All violations found during validation
    /// </summary>
    public List<Violation> Violations { get; init; } = new();

    /// <summary>
    /// Summary statistics
    /// </summary>
    public ValidationSummary Summary { get; init; } = new();

    /// <summary>
    /// Whether the validation completed successfully
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception details if validation failed
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Information about the ruleset used for validation
/// </summary>
public class RuleSetInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Version { get; init; }
    public string? EffectiveFrom { get; init; }
    public string? Owner { get; init; }
    public int RuleCount { get; init; }
}

/// <summary>
/// Summary statistics for a validation run
/// </summary>
public class ValidationSummary
{
    /// <summary>
    /// Total number of entities processed
    /// </summary>
    public int EntitiesProcessed { get; set; }

    /// <summary>
    /// Number of entities with violations
    /// </summary>
    public int EntitiesWithViolations { get; set; }

    /// <summary>
    /// Total number of violations found
    /// </summary>
    public int TotalViolations { get; set; }

    /// <summary>
    /// Violations grouped by severity
    /// </summary>
    public Dictionary<Severity, int> ViolationsBySeverity { get; set; } = new();

    /// <summary>
    /// Violations grouped by entity type
    /// </summary>
    public Dictionary<string, int> ViolationsByEntityType { get; set; } = new();

    /// <summary>
    /// Violations grouped by rule ID
    /// </summary>
    public Dictionary<string, int> ViolationsByRule { get; set; } = new();

    /// <summary>
    /// Number of violations that can be automatically repaired
    /// </summary>
    public int RepairableViolations { get; set; }
}
