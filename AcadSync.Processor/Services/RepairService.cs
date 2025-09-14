using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Linq;
using AcadSync.Processor.Configuration;
using AcadSync.Processor.Interfaces;
using AcadSync.Processor.Models.Results;
using AcadSync.Audit.Interfaces;
using AcadSync.Audit.Extensions;
using AcadSync.Audit.Models;

namespace AcadSync.Processor.Services;

/// <summary>
/// Service for repairing violations by applying fixes to the database
/// </summary>
public class RepairService : IRepairService
{
    private readonly IExtPropRepository _repository;
    private readonly IAuditRepository _auditRepository;
    private readonly IRuleLoader _ruleLoader;
    private readonly IRuleEngine _ruleEngine;
    private readonly ProcessorOptions _options;
    private readonly ILogger<RepairService> _logger;

    public RepairService(
        IExtPropRepository repository,
        IAuditRepository auditRepository,
        IRuleLoader ruleLoader,
        IRuleEngine ruleEngine,
        IOptions<ProcessorOptions> options,
        ILogger<RepairService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _ruleLoader = ruleLoader ?? throw new ArgumentNullException(nameof(ruleLoader));
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RepairResult> RepairViolationsAsync(IEnumerable<Violation> violations, int staffId = 1)
    {
        var result = new RepairResult { StaffId = staffId };
        var violationsList = violations.ToList();
        var repairableViolations = GetRepairableViolations(violationsList).ToList();

        result.TotalViolationsAttempted = repairableViolations.Count;

        _logger.LogInformation("Starting repair process for {RepairableCount} violations out of {TotalCount} total (Staff ID: {StaffId})", 
            repairableViolations.Count, violationsList.Count, staffId);

        if (!repairableViolations.Any())
        {
            _logger.LogInformation("No repairable violations found - nothing to repair");
            result.EndTime = DateTimeOffset.UtcNow;
            return result;
        }

        foreach (var violation in repairableViolations)
        {
            try
            {
                var success = await RepairViolationAsync(violation, staffId);
                
                if (success)
                {
                    result.SuccessfulRepairs++;
                    result.SuccessfulRepairDetails.Add(new RepairDetail
                    {
                        RuleId = violation.RuleId,
                        EntityType = violation.EntityType,
                        EntityId = violation.EntityId,
                        PropertyCode = violation.PropertyCode,
                        OldValue = violation.CurrentValue,
                        NewValue = violation.ProposedValue,
                        Action = violation.Action
                    });

                    _logger.LogDebug("Successfully repaired {EntityType}#{EntityId}.{PropertyCode}: '{OldValue}' → '{NewValue}'",
                        violation.EntityType, violation.EntityId, violation.PropertyCode,
                        violation.CurrentValue, violation.ProposedValue);
                }
                else
                {
                    result.FailedRepairs++;
                    result.FailedRepairDetails.Add(new RepairFailure
                    {
                        RuleId = violation.RuleId,
                        EntityType = violation.EntityType,
                        EntityId = violation.EntityId,
                        PropertyCode = violation.PropertyCode,
                        AttemptedValue = violation.ProposedValue,
                        ErrorMessage = "Repair operation returned false"
                    });
                }
            }
            catch (Exception ex)
            {
                result.FailedRepairs++;
                result.FailedRepairDetails.Add(new RepairFailure
                {
                    RuleId = violation.RuleId,
                    EntityType = violation.EntityType,
                    EntityId = violation.EntityId,
                    PropertyCode = violation.PropertyCode,
                    AttemptedValue = violation.ProposedValue,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });

                _logger.LogError(ex, "Failed to repair {EntityType}#{EntityId}.{PropertyCode}",
                    violation.EntityType, violation.EntityId, violation.PropertyCode);
            }
        }

        result.EndTime = DateTimeOffset.UtcNow;

        _logger.LogInformation("Repair process completed in {Duration}ms. Success: {SuccessCount}, Failed: {FailedCount}, Success Rate: {SuccessRate:F1}%",
            result.Duration.TotalMilliseconds, result.SuccessfulRepairs, result.FailedRepairs, result.SuccessRate);

        return result;
    }

    public async Task<bool> RepairViolationAsync(Violation violation, int staffId = 1)
    {
        if (!CanRepairViolation(violation))
        {
            _logger.LogWarning("Violation cannot be repaired: {RuleId} for {EntityType}#{EntityId}.{PropertyCode}",
                violation.RuleId, violation.EntityType, violation.EntityId, violation.PropertyCode);
            return false;
        }

        try
        {
            _logger.LogDebug("Attempting to repair {EntityType}#{EntityId}.{PropertyCode} with value '{ProposedValue}'",
                violation.EntityType, violation.EntityId, violation.PropertyCode, violation.ProposedValue);

            // Apply the repair to the database
            await _repository.UpsertExtPropertyAsync(
                violation.EntityType,
                violation.EntityId,
                violation.PropertyCode,
                violation.ProposedValue,
                staffId
            );

            // Write audit record for successful repair (ensure datetime formatting when applicable)
            var successAudit = await BuildAuditEntryAsync(violation);
            await _auditRepository.WriteAuditAsync(
                successAudit,
                staffId,
                $"Auto-repaired by AcadSync: {violation.RuleId}"
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to repair violation: {RuleId} for {EntityType}#{EntityId}.{PropertyCode}",
                violation.RuleId, violation.EntityType, violation.EntityId, violation.PropertyCode);

            // Write audit record for failed repair
            try
            {
                var failAudit = await BuildAuditEntryAsync(violation);
                await _auditRepository.WriteAuditAsync(
                    failAudit,
                    staffId,
                    $"Repair failed: {ex.Message}"
                );
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "Failed to write audit record for failed repair");
            }

            return false;
        }
    }

    public bool CanRepairViolation(Violation violation)
    {
        // Check if the violation has a repair action
        if (string.IsNullOrEmpty(violation.Action) || !violation.Action.Contains("repair"))
            return false;

        // Check if there's a proposed value to apply
        if (string.IsNullOrEmpty(violation.ProposedValue))
            return false;

        // Additional business rules can be added here
        // For example, certain severity levels might not be auto-repairable
        if (violation.Severity == Severity.block)
        {
            _logger.LogDebug("Blocking violations are not auto-repairable: {RuleId}", violation.RuleId);
            return false;
        }

        return true;
    }

    public IEnumerable<Violation> GetRepairableViolations(IEnumerable<Violation> violations)
    {
        return violations.Where(CanRepairViolation);
    }

    public async Task<ValidationAndRepairResult> ValidateAndRepairAsync(IEnumerable<IEntityProjection> entities, int staffId = 1)
    {
        var result = new ValidationAndRepairResult();
        var entitiesList = entities.ToList();

        _logger.LogInformation("Starting combined validation and repair process for {EntityCount} entities (Staff ID: {StaffId})", 
            entitiesList.Count, staffId);

        try
        {
            // Phase 1: Validation
            _logger.LogInformation("Phase 1: Validation");
            
            // Load rules and perform validation directly
            var doc = await _ruleLoader.LoadRulesAsync();
            var violations = await _ruleEngine.EvaluateAsync(doc, entitiesList);
            var violationsList = violations.ToList();

            // Create validation result
            result.ValidationResult = new ValidationResult { Mode = EprlMode.repair };
            result.ValidationResult.RuleSet = new RuleSetInfo
            {
                Id = doc.Ruleset.Id,
                Name = doc.Ruleset.Name,
                Version = doc.Ruleset.Version,
                EffectiveFrom = doc.Ruleset.EffectiveFrom,
                Owner = doc.Ruleset.Owner,
                RuleCount = doc.Rules.Count
            };
            result.ValidationResult.Violations.AddRange(violationsList);
            
            // Calculate summary statistics
            result.ValidationResult.Summary.EntitiesProcessed = entitiesList.Count;
            result.ValidationResult.Summary.TotalViolations = violationsList.Count;
            result.ValidationResult.Summary.EntitiesWithViolations = violationsList.Select(v => $"{v.EntityType}:{v.EntityId}").Distinct().Count();
            result.ValidationResult.Summary.ViolationsBySeverity = violationsList.GroupBy(v => v.Severity).ToDictionary(g => g.Key, g => g.Count());
            result.ValidationResult.Summary.ViolationsByEntityType = violationsList.GroupBy(v => v.EntityType).ToDictionary(g => g.Key, g => g.Count());
            result.ValidationResult.Summary.ViolationsByRule = violationsList.GroupBy(v => v.RuleId).ToDictionary(g => g.Key, g => g.Count());
            result.ValidationResult.Summary.RepairableViolations = violationsList.Count(v => v.Action.Contains("repair") && !string.IsNullOrEmpty(v.ProposedValue));
            result.ValidationResult.EndTime = DateTimeOffset.UtcNow;

            // Phase 2: Repair (only if violations were found)
            if (violationsList.Any())
            {
                _logger.LogInformation("Phase 2: Repair ({ViolationCount} violations found)", violationsList.Count);
                result.RepairResult = await RepairViolationsAsync(violationsList, staffId);
            }
            else
            {
                _logger.LogInformation("No violations found during validation - skipping repair phase");
            }

            _logger.LogInformation("Combined validation and repair completed successfully in {Duration}ms", 
                result.TotalDuration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Combined validation and repair process failed");
            
            // If validation succeeded but repair failed, we still want to return the validation results
            if (result.ValidationResult.IsSuccess && result.RepairResult != null)
            {
                result.RepairResult.IsSuccess = false;
                result.RepairResult.ErrorMessage = ex.Message;
                result.RepairResult.Exception = ex;
            }
            else
            {
                // If validation failed, mark the entire operation as failed
                result.ValidationResult.IsSuccess = false;
                result.ValidationResult.ErrorMessage = ex.Message;
                result.ValidationResult.Exception = ex;
            }

            return result;
        }
    }

    private async Task<AuditEntry> BuildAuditEntryAsync(Violation violation)
    {
        // Determine if the property is datetime by looking up the definition
        bool isDateType = false;
        try
        {
            var defs = await _repository.GetPropertyDefinitionsAsync(violation.EntityType);
            var def = defs.FirstOrDefault(d => string.Equals(d.PropertyCode, violation.PropertyCode, StringComparison.OrdinalIgnoreCase));
            if (def != null)
            {
                // DataType maps to PropertyType column
                isDateType = string.Equals(def.DataType, "datetime", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // If lookup fails, default to not formatting
            isDateType = false;
        }

        string? oldVal = violation.CurrentValue;
        string? newVal = violation.ProposedValue;

        if (isDateType)
        {
            oldVal = FormatDateIfParsable(oldVal);
            newVal = FormatDateIfParsable(newVal);
        }

        return new AuditEntry(
            violation.RuleId,
            violation.EntityType,
            violation.EntityId,
            violation.PropertyCode,
            oldVal,
            newVal,
            violation.Action,
            violation.Severity.ToString()
        );
    }

    private static string? FormatDateIfParsable(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Accept both ISO date and datetime strings
        if (DateTime.TryParse(input.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            // Normalize to midnight as per requirement examples
            var normalized = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, 0, DateTimeKind.Unspecified);
            return normalized.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        return input;
    }
}
