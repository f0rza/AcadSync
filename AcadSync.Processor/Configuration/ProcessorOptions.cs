namespace AcadSync.Processor.Configuration;

/// <summary>
/// Main configuration options for the AcadSync Processor
/// </summary>
public class ProcessorOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "AcadSync:Processor";

    /// <summary>
    /// Path to the rules file (YAML)
    /// </summary>
    public string RulesFilePath { get; set; } = "rules.yaml";

    /// <summary>
    /// Database connection string
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Default staff ID for automated operations
    /// </summary>
    public int DefaultStaffId { get; set; } = 1;

    /// <summary>
    /// Cache configuration
    /// </summary>
    public CacheOptions Cache { get; set; } = new();

    /// <summary>
    /// Logging configuration
    /// </summary>
    public LoggingOptions Logging { get; set; } = new();

    /// <summary>
    /// Performance and timeout settings
    /// </summary>
    public PerformanceOptions Performance { get; set; } = new();
}

/// <summary>
/// Cache configuration options
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Enable rule caching
    /// </summary>
    public bool EnableRuleCache { get; set; } = true;

    /// <summary>
    /// Rule cache expiration in minutes
    /// </summary>
    public int RuleCacheExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Enable property definition caching
    /// </summary>
    public bool EnablePropertyCache { get; set; } = true;

    /// <summary>
    /// Property cache expiration in minutes
    /// </summary>
    public int PropertyCacheExpirationMinutes { get; set; } = 60;
}

/// <summary>
/// Logging configuration options
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Enable structured logging with correlation IDs
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Log detailed violation information
    /// </summary>
    public bool LogViolationDetails { get; set; } = true;

    /// <summary>
    /// Log performance metrics
    /// </summary>
    public bool LogPerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Minimum log level for processor operations
    /// </summary>
    public string MinimumLogLevel { get; set; } = "Information";
}

/// <summary>
/// Performance and timeout configuration
/// </summary>
public class PerformanceOptions
{
    /// <summary>
    /// Database command timeout in seconds
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of entities to process in a single batch
    /// </summary>
    public int MaxBatchSize { get; set; } = 1000;

    /// <summary>
    /// Enable parallel processing of entities
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;

    /// <summary>
    /// Maximum degree of parallelism
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}
