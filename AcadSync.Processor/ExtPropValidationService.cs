using Microsoft.Extensions.Logging;
using AcadSync.Audit.Interfaces;
using AcadSync.Audit.Extensions;

namespace AcadSync.Processor;

/// <summary>
/// Main service for Extended Property validation, simulation, and repair operations
/// </summary>
public class ExtPropValidationService
{
    private readonly IExtPropRepository _repository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<ExtPropValidationService> _logger;

    public ExtPropValidationService(IExtPropRepository repository, IAuditRepository auditRepository, ILogger<ExtPropValidationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
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
            // Load rules
            var rulesPath = Path.Combine(Directory.GetCurrentDirectory(), "rules.yaml");
            if (!File.Exists(rulesPath))
            {
                throw new FileNotFoundException($"Rules file not found: {rulesPath}");
            }

            var yamlContent = await File.ReadAllTextAsync(rulesPath);
            var doc = EprlLoader.LoadFromYaml(yamlContent);

            _logger.LogInformation("Loaded ruleset: {Name} v{Version} with {RuleCount} rules", 
                doc.Ruleset.Name, doc.Ruleset.Version, doc.Rules.Count);

            // Load entities from database
            var students = await _repository.GetStudentsAsync();
            var documents = await _repository.GetDocumentsAsync();

            _logger.LogInformation("Loaded {StudentCount} students and {DocumentCount} documents from database", 
                students.Count, documents.Count);

            // Combine all entities
            var allEntities = new List<IEntityProjection>();
            allEntities.AddRange(students);
            allEntities.AddRange(documents);

            // Evaluate rules
            var violations = Evaluator.EvaluateWithContext(doc, allEntities).ToList();

            _logger.LogInformation("Found {ViolationCount} violations", violations.Count);

            // Log summary by severity
            var summary = violations.GroupBy(v => v.Severity)
                .ToDictionary(g => g.Key, g => g.Count());
            
            foreach (var (severity, count) in summary)
            {
                _logger.LogInformation("  {Severity}: {Count}", severity, count);
            }

            return violations;
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
            // First, get all violations
            var violations = await ValidateAllAsync(EprlMode.repair);

            if (!violations.Any())
            {
                _logger.LogInformation("No violations found - nothing to repair");
                return violations;
            }

            // Filter to only violations that can be repaired
            var repairableViolations = violations
                .Where(v => v.Action.Contains("repair") && !string.IsNullOrEmpty(v.ProposedValue))
                .ToList();

            _logger.LogInformation("Found {RepairableCount} repairable violations out of {TotalCount} total", 
                repairableViolations.Count, violations.Count);

            // Apply repairs
            var repairedCount = 0;
            foreach (var violation in repairableViolations)
            {
                try
                {
                    await _repository.UpsertExtPropertyAsync(
                        violation.EntityType,
                        violation.EntityId,
                        violation.PropertyCode,
                        violation.ProposedValue,
                        staffId
                    );

                    // Write audit record
                    await _auditRepository.WriteAuditAsync(violation.ToAuditEntry(), staffId, "Auto-repaired by AcadSync");

                    repairedCount++;
                    _logger.LogDebug("Repaired {EntityType}#{EntityId}.{PropertyCode}: '{OldValue}' → '{NewValue}'",
                        violation.EntityType, violation.EntityId, violation.PropertyCode,
                        violation.CurrentValue, violation.ProposedValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to repair {EntityType}#{EntityId}.{PropertyCode}",
                        violation.EntityType, violation.EntityId, violation.PropertyCode);
                    
                    // Write audit record for failed repair
                    await _auditRepository.WriteAuditAsync(violation.ToAuditEntry(), staffId, $"Repair failed: {ex.Message}");
                }
            }

            _logger.LogInformation("Successfully repaired {RepairedCount} violations", repairedCount);
            return violations;
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
            var isConnected = await _repository.TestConnectionAsync();
            _logger.LogInformation("Database connection test: {Result}", isConnected ? "SUCCESS" : "FAILED");
            return isConnected;
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

    /// <summary>
    /// Validate specific entities (for targeted validation)
    /// </summary>
    public async Task<List<Violation>> ValidateEntitiesAsync(IEnumerable<IEntityProjection> entities, EprlMode mode = EprlMode.validate)
    {
        _logger.LogInformation("Starting targeted validation for {EntityCount} entities in {Mode} mode", 
            entities.Count(), mode);

        try
        {
            // Load rules
            var rulesPath = Path.Combine(Directory.GetCurrentDirectory(), "rules.yaml");
            if (!File.Exists(rulesPath))
            {
                throw new FileNotFoundException($"Rules file not found: {rulesPath}");
            }

            var yamlContent = await File.ReadAllTextAsync(rulesPath);
            var doc = EprlLoader.LoadFromYaml(yamlContent);

            // Evaluate rules against provided entities
            var violations = Evaluator.EvaluateWithContext(doc, entities).ToList();

            _logger.LogInformation("Found {ViolationCount} violations in targeted validation", violations.Count);
            return violations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Targeted validation failed");
            throw;
        }
    }
}
