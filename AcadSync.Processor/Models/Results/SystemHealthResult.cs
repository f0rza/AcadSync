namespace AcadSync.Processor.Models.Results;

/// <summary>
/// Result of a system health check
/// </summary>
public class SystemHealthResult
{
    /// <summary>
    /// Unique identifier for this health check
    /// </summary>
    public string HealthCheckId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when health check was performed
    /// </summary>
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Overall system health status
    /// </summary>
    public HealthStatus OverallStatus { get; set; } = HealthStatus.Healthy;

    /// <summary>
    /// Individual component health checks
    /// </summary>
    public List<ComponentHealth> Components { get; init; } = new();

    /// <summary>
    /// Configuration validation results
    /// </summary>
    public ConfigurationHealth Configuration { get; init; } = new();

    /// <summary>
    /// General error message if health check failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception details if health check failed
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Whether all components are healthy
    /// </summary>
    public bool IsHealthy => OverallStatus == HealthStatus.Healthy;

    /// <summary>
    /// Get health status summary
    /// </summary>
    public string GetSummary()
    {
        var healthyCount = Components.Count(c => c.Status == HealthStatus.Healthy);
        var totalCount = Components.Count;
        return $"Overall: {OverallStatus}, Components: {healthyCount}/{totalCount} healthy";
    }
}

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Health check result for an individual component
/// </summary>
public class ComponentHealth
{
    public string Name { get; init; } = "";
    public HealthStatus Status { get; set; } = HealthStatus.Healthy;
    public string? Description { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public Dictionary<string, object> Data { get; init; } = new();
}

/// <summary>
/// Configuration validation health check
/// </summary>
public class ConfigurationHealth
{
    public bool IsValid { get; set; } = true;
    public List<string> ValidationErrors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public Dictionary<string, object> ConfigurationValues { get; init; } = new();
}
