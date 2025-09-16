using Microsoft.Extensions.Logging;
using AcadSync.Processor.Interfaces;
using AcadSync.Processor.Models.Results;

namespace AcadSync.Processor.Services;

/// <summary>
/// Main validation orchestration service
/// </summary>
public class ValidationOrchestrator : IValidationService
{
    private readonly IRuleLoader _ruleLoader;
    private readonly IRuleEngine _ruleEngine;
    private readonly IEntityService _entityService;
    private readonly IExtPropRepository _repository;
    private readonly ILogger<ValidationOrchestrator> _logger;

    public ValidationOrchestrator(
        IRuleLoader ruleLoader,
        IRuleEngine ruleEngine,
        IEntityService entityService,
        IExtPropRepository repository,
        ILogger<ValidationOrchestrator> logger)
    {
        _ruleLoader = ruleLoader ?? throw new ArgumentNullException(nameof(ruleLoader));
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _entityService = entityService ?? throw new ArgumentNullException(nameof(entityService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ValidationResult> ValidateAllAsync(EprlMode mode = EprlMode.validate)
    {
        var result = new ValidationResult { Mode = mode };
        
        try
        {
            _logger.LogInformation("Starting validation in {Mode} mode", mode);

            // Load rules
            var doc = await _ruleLoader.LoadRulesAsync();
            result.RuleSet = new RuleSetInfo
            {
                Id = doc.Ruleset.Id,
                Name = doc.Ruleset.Name,
                Version = doc.Ruleset.Version,
                EffectiveFrom = doc.Ruleset.EffectiveFrom,
                Owner = doc.Ruleset.Owner,
                RuleCount = doc.Rules.Count
            };

            _logger.LogInformation("Loaded ruleset: {Name} v{Version} with {RuleCount} rules", 
                doc.Ruleset.Name, doc.Ruleset.Version, doc.Rules.Count);

            // Load all entities
            var entities = await _entityService.GetAllEntitiesAsync();
            var entitiesList = entities.ToList();

            _logger.LogInformation("Loaded {EntityCount} entities for validation", entitiesList.Count);

            // Perform validation
            var violations = await _ruleEngine.EvaluateAsync(doc, entitiesList);
            result.Violations.AddRange(violations);

            // Calculate summary statistics
            CalculateSummary(result, entitiesList);

            result.EndTime = DateTimeOffset.UtcNow;
            _logger.LogInformation("Validation completed in {Duration}ms. Found {ViolationCount} violations", 
                result.Duration.TotalMilliseconds, result.Violations.Count);

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            result.EndTime = DateTimeOffset.UtcNow;

            _logger.LogError(ex, "Validation failed after {Duration}ms", result.Duration.TotalMilliseconds);
            return result;
        }
    }

    public async Task<ValidationResult> ValidateEntitiesAsync(IEnumerable<IEntityProjection> entities, EprlMode mode = EprlMode.validate)
    {
        var result = new ValidationResult { Mode = mode };
        var entitiesList = entities.ToList();

        try
        {
            _logger.LogInformation("Starting targeted validation for {EntityCount} entities in {Mode} mode", 
                entitiesList.Count, mode);

            // Load rules
            var doc = await _ruleLoader.LoadRulesAsync();
            result.RuleSet = new RuleSetInfo
            {
                Id = doc.Ruleset.Id,
                Name = doc.Ruleset.Name,
                Version = doc.Ruleset.Version,
                EffectiveFrom = doc.Ruleset.EffectiveFrom,
                Owner = doc.Ruleset.Owner,
                RuleCount = doc.Rules.Count
            };

            // Perform validation
            var violations = await _ruleEngine.EvaluateAsync(doc, entitiesList);
            result.Violations.AddRange(violations);

            // Calculate summary statistics
            CalculateSummary(result, entitiesList);

            result.EndTime = DateTimeOffset.UtcNow;
            _logger.LogInformation("Targeted validation completed in {Duration}ms. Found {ViolationCount} violations", 
                result.Duration.TotalMilliseconds, result.Violations.Count);

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            result.EndTime = DateTimeOffset.UtcNow;

            _logger.LogError(ex, "Targeted validation failed after {Duration}ms", result.Duration.TotalMilliseconds);
            return result;
        }
    }

    public async Task<ValidationResult> ValidateWithCustomRulesAsync(string rulesFilePath, EprlMode mode = EprlMode.validate)
    {
        var result = new ValidationResult { Mode = mode };

        try
        {
            _logger.LogInformation("Starting validation with custom rules from {RulesFilePath} in {Mode} mode", 
                rulesFilePath, mode);

            // Load custom rules
            var doc = await _ruleLoader.LoadRulesFromFileAsync(rulesFilePath);
            result.RuleSet = new RuleSetInfo
            {
                Id = doc.Ruleset.Id,
                Name = doc.Ruleset.Name,
                Version = doc.Ruleset.Version,
                EffectiveFrom = doc.Ruleset.EffectiveFrom,
                Owner = doc.Ruleset.Owner,
                RuleCount = doc.Rules.Count
            };

            // Load all entities
            var entities = await _entityService.GetAllEntitiesAsync();
            var entitiesList = entities.ToList();

            // Perform validation
            var violations = await _ruleEngine.EvaluateAsync(doc, entitiesList);
            result.Violations.AddRange(violations);

            // Calculate summary statistics
            CalculateSummary(result, entitiesList);

            result.EndTime = DateTimeOffset.UtcNow;
            _logger.LogInformation("Custom rules validation completed in {Duration}ms. Found {ViolationCount} violations", 
                result.Duration.TotalMilliseconds, result.Violations.Count);

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            result.EndTime = DateTimeOffset.UtcNow;

            _logger.LogError(ex, "Custom rules validation failed after {Duration}ms", result.Duration.TotalMilliseconds);
            return result;
        }
    }

    public async Task<IEnumerable<ExtPropertyDefinition>> GetPropertyDefinitionsAsync(string entityType)
    {
        _logger.LogInformation("Retrieving property definitions for {EntityType}", entityType);

        try
        {
            var definitions = await _repository.GetPropertyDefinitionsAsync(entityType);
            _logger.LogInformation("Found {Count} property definitions for {EntityType}", 
                definitions.Count, entityType);
            return definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve property definitions for {EntityType}", entityType);
            throw;
        }
    }

    public async Task<SystemHealthResult> TestSystemHealthAsync()
    {
        var result = new SystemHealthResult();

        try
        {
            _logger.LogInformation("Starting system health check");

            // Test database connectivity
            var dbHealth = await TestDatabaseHealthAsync();
            result.Components.Add(dbHealth);

            // Test rule loading
            var ruleHealth = await TestRuleHealthAsync();
            result.Components.Add(ruleHealth);

            // Test entity retrieval
            var entityHealth = await TestEntityHealthAsync();
            result.Components.Add(entityHealth);

            // Determine overall health
            result.OverallStatus = result.Components.All(c => c.Status == HealthStatus.Healthy) 
                ? HealthStatus.Healthy 
                : result.Components.Any(c => c.Status == HealthStatus.Unhealthy) 
                    ? HealthStatus.Unhealthy 
                    : HealthStatus.Degraded;

            _logger.LogInformation("System health check completed: {Status}", result.OverallStatus);
            return result;
        }
        catch (Exception ex)
        {
            result.OverallStatus = HealthStatus.Unhealthy;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;

            _logger.LogError(ex, "System health check failed");
            return result;
        }
    }

    private void CalculateSummary(ValidationResult result, List<IEntityProjection> entities)
    {
        result.Summary.EntitiesProcessed = entities.Count;
        result.Summary.TotalViolations = result.Violations.Count;
        result.Summary.EntitiesWithViolations = result.Violations.Select(v => $"{v.EntityType}:{v.EntityId}").Distinct().Count();

        // Group by severity
        result.Summary.ViolationsBySeverity = result.Violations
            .GroupBy(v => v.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

        // Group by entity type
        result.Summary.ViolationsByEntityType = result.Violations
            .GroupBy(v => v.EntityType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Group by rule
        result.Summary.ViolationsByRule = result.Violations
            .GroupBy(v => v.RuleId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Count repairable violations
        result.Summary.RepairableViolations = result.Violations
            .Count(v => v.Action.Contains("repair") && !string.IsNullOrEmpty(v.ProposedValue));

        // Log summary
        _logger.LogInformation("Validation summary: {EntitiesProcessed} entities processed, {TotalViolations} violations found, {RepairableViolations} repairable",
            result.Summary.EntitiesProcessed, result.Summary.TotalViolations, result.Summary.RepairableViolations);

        foreach (var (severity, count) in result.Summary.ViolationsBySeverity)
        {
            _logger.LogInformation("  {Severity}: {Count}", severity, count);
        }
    }

    private async Task<ComponentHealth> TestDatabaseHealthAsync()
    {
        var component = new ComponentHealth { Name = "Database" };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var isConnected = await _entityService.TestConnectionAsync();
            stopwatch.Stop();

            component.Status = isConnected ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            component.Description = isConnected ? "Database connection successful" : "Database connection failed";
            component.ResponseTime = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            component.Status = HealthStatus.Unhealthy;
            component.ErrorMessage = ex.Message;
            component.Exception = ex;
            component.ResponseTime = stopwatch.Elapsed;
        }

        return component;
    }

    private async Task<ComponentHealth> TestRuleHealthAsync()
    {
        var component = new ComponentHealth { Name = "Rules" };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var doc = await _ruleLoader.LoadRulesAsync();
            stopwatch.Stop();

            component.Status = HealthStatus.Healthy;
            component.Description = $"Rules loaded successfully: {doc.Ruleset.Name} v{doc.Ruleset.Version}";
            component.ResponseTime = stopwatch.Elapsed;
            component.Data["RulesetName"] = doc.Ruleset.Name;
            component.Data["RulesetVersion"] = doc.Ruleset.Version;
            component.Data["RuleCount"] = doc.Rules.Count;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            component.Status = HealthStatus.Unhealthy;
            component.ErrorMessage = ex.Message;
            component.Exception = ex;
            component.ResponseTime = stopwatch.Elapsed;
        }

        return component;
    }

    private async Task<ComponentHealth> TestEntityHealthAsync()
    {
        var component = new ComponentHealth { Name = "Entities" };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var entities = await _entityService.GetAllEntitiesAsync();
            var entityList = entities.ToList();
            stopwatch.Stop();

            component.Status = HealthStatus.Healthy;
            component.Description = $"Entities retrieved successfully: {entityList.Count} total";
            component.ResponseTime = stopwatch.Elapsed;
            component.Data["TotalEntities"] = entityList.Count;
            component.Data["StudentCount"] = entityList.Count(e => e.EntityType == "Student");
            component.Data["DocumentCount"] = entityList.Count(e => e.EntityType == "Document");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            component.Status = HealthStatus.Unhealthy;
            component.ErrorMessage = ex.Message;
            component.Exception = ex;
            component.ResponseTime = stopwatch.Elapsed;
        }

        return component;
    }
}
