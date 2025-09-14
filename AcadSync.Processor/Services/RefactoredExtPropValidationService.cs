using Microsoft.Extensions.Logging;
using AcadSync.Processor.Interfaces;
using AcadSync.Processor.Models.Results;

namespace AcadSync.Processor.Services;

/// <summary>
/// Refactored Extended Property validation service using the new architecture
/// This replaces the original ExtPropValidationService with better separation of concerns
/// </summary>
public class RefactoredExtPropValidationService
{
    private readonly IValidationService _validationService;
    private readonly IRepairService _repairService;
    private readonly IRevertService _revertService;
    private readonly IEntityService _entityService;
    private readonly ILogger<RefactoredExtPropValidationService> _logger;

    public RefactoredExtPropValidationService(
        IValidationService validationService,
        IRepairService repairService,
        IRevertService revertService,
        IEntityService entityService,
        ILogger<RefactoredExtPropValidationService> logger)
    {
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _repairService = repairService ?? throw new ArgumentNullException(nameof(repairService));
        _revertService = revertService ?? throw new ArgumentNullException(nameof(revertService));
        _entityService = entityService ?? throw new ArgumentNullException(nameof(entityService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validate all entities against rules without making changes
    /// </summary>
    public async Task<List<Violation>> ValidateAllAsync(EprlMode mode = EprlMode.validate)
    {
        _logger.LogInformation("Starting validation in {Mode} mode", mode);

        try
        {
            var result = await _validationService.ValidateAllAsync(mode);
            
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Validation failed: {result.ErrorMessage}", result.Exception);
            }

            _logger.LogInformation("Validation completed successfully. Found {ViolationCount} violations", result.Violations.Count);
            return result.Violations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed");
            throw;
        }
    }

    /// <summary>
    /// Validate and repair violations by applying fixes to the database
    /// </summary>
    public async Task<List<Violation>> ValidateAndRepairAsync(int staffId = 1)
    {
        _logger.LogInformation("Starting validation and repair process");

        try
        {
            // Get all entities
            var entities = await _entityService.GetAllEntitiesAsync();
            
            // Perform validation and repair
            var result = await _repairService.ValidateAndRepairAsync(entities, staffId);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Validation and repair failed: {result.ErrorMessage}");
            }

            _logger.LogInformation("Validation and repair completed successfully");
            
            if (result.RepairResult != null)
            {
                _logger.LogInformation("Repair summary: {SuccessfulRepairs} successful, {FailedRepairs} failed, {SuccessRate:F1}% success rate",
                    result.RepairResult.SuccessfulRepairs, result.RepairResult.FailedRepairs, result.RepairResult.SuccessRate);
            }

            return result.ValidationResult.Violations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation and repair process failed");
            throw;
        }
    }

    /// <summary>
    /// Test database connectivity
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var healthResult = await _validationService.TestSystemHealthAsync();
            var dbComponent = healthResult.Components.FirstOrDefault(c => c.Name == "Database");
            
            var isHealthy = dbComponent?.Status == HealthStatus.Healthy;
            _logger.LogInformation("Database connection test: {Result}", isHealthy ? "SUCCESS" : "FAILED");
            
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection test failed");
            return false;
        }
    }

    /// <summary>
    /// Get extended property definitions for diagnostics
    /// </summary>
    public async Task<List<ExtPropertyDefinition>> GetPropertyDefinitionsAsync(string entityType)
    {
        _logger.LogInformation("Retrieving property definitions for {EntityType}", entityType);
        
        try
        {
            var definitions = await _validationService.GetPropertyDefinitionsAsync(entityType);
            var definitionsList = definitions.ToList();
            
            _logger.LogInformation("Found {Count} property definitions for {EntityType}", 
                definitionsList.Count, entityType);
            
            return definitionsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve property definitions for {EntityType}", entityType);
            throw;
        }
    }

    /// <summary>
    /// Validate specific entities (for targeted validation)
    /// </summary>
    public async Task<List<Violation>> ValidateEntitiesAsync(IEnumerable<IEntityProjection> entities, EprlMode mode = EprlMode.validate)
    {
        _logger.LogInformation("Starting targeted validation for {EntityCount} entities in {Mode} mode", 
            entities.Count(), mode);

        try
        {
            var result = await _validationService.ValidateEntitiesAsync(entities, mode);
            
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Targeted validation failed: {result.ErrorMessage}", result.Exception);
            }

            _logger.LogInformation("Targeted validation completed successfully. Found {ViolationCount} violations", result.Violations.Count);
            return result.Violations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Targeted validation failed");
            throw;
        }
    }

    /// <summary>
    /// Get comprehensive system health information
    /// </summary>
    public async Task<SystemHealthResult> GetSystemHealthAsync()
    {
        _logger.LogInformation("Performing comprehensive system health check");

        try
        {
            var healthResult = await _validationService.TestSystemHealthAsync();
            
            _logger.LogInformation("System health check completed: {Status} - {Summary}", 
                healthResult.OverallStatus, healthResult.GetSummary());

            return healthResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System health check failed");
            throw;
        }
    }

    /// <summary>
    /// Get detailed validation result with comprehensive statistics
    /// </summary>
    public async Task<ValidationResult> GetDetailedValidationResultAsync(EprlMode mode = EprlMode.validate)
    {
        _logger.LogInformation("Getting detailed validation result in {Mode} mode", mode);

        try
        {
            var result = await _validationService.ValidateAllAsync(mode);
            
            _logger.LogInformation("Detailed validation completed in {Duration}ms", result.Duration.TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get detailed validation result");
            throw;
        }
    }

    /// <summary>
    /// Get detailed repair result with comprehensive statistics
    /// </summary>
    public async Task<ValidationAndRepairResult> GetDetailedRepairResultAsync(int staffId = 1)
    {
        _logger.LogInformation("Getting detailed repair result");

        try
        {
            var entities = await _entityService.GetAllEntitiesAsync();
            var result = await _repairService.ValidateAndRepairAsync(entities, staffId);

            _logger.LogInformation("Detailed repair completed in {Duration}ms", result.TotalDuration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get detailed repair result");
            throw;
        }
    }

    /// <summary>
    /// Revert repairs based on time range (simplified implementation)
    /// </summary>
    public async Task<RepairResult> RevertRepairsAsync(DateTimeOffset fromDate, bool force = false, int staffId = 1, bool dryRun = false)
    {
        _logger.LogInformation("Starting revert operation for repairs since {FromDate}", fromDate);

        try
        {
            var result = await _revertService.RevertByFilterAsync(
                from: fromDate,
                force: force,
                staffId: staffId,
                dryRun: dryRun);

            _logger.LogInformation("Revert operation completed: {SuccessfulRepairs} successful, {FailedRepairs} failed",
                result.SuccessfulRepairs, result.FailedRepairs);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revert operation failed");
            throw;
        }
    }
}
