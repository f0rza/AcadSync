using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Linq;
using AcadSync.Processor.Interfaces;
using AcadSync.Processor.Models.Results;
using AcadSync.Audit.Interfaces;
using AcadSync.Audit.Models;

namespace AcadSync.Processor.Services;

/// <summary>
/// Service for reverting repair operations with safety guards
/// </summary>
public class RevertService : IRevertService
{
    private readonly IAuditRepository _auditRepository;
    private readonly IExtPropRepository _extPropRepository;
    private readonly ILogger<RevertService> _logger;

    public RevertService(
        IAuditRepository auditRepository,
        IExtPropRepository extPropRepository,
        ILogger<RevertService> logger)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _extPropRepository = extPropRepository ?? throw new ArgumentNullException(nameof(extPropRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RepairResult> RevertAsync(IEnumerable<AuditEntry> repairs, bool force = false, int staffId = 1, bool dryRun = false)
    {
        var result = new RepairResult { StaffId = staffId };
        var repairsList = repairs.ToList();

        _logger.LogInformation("Starting revert operation for {RepairCount} repairs (Staff ID: {StaffId}, Force: {Force}, DryRun: {DryRun})",
            repairsList.Count, staffId, force, dryRun);

        if (!repairsList.Any())
        {
            _logger.LogInformation("No repairs to revert");
            result.EndTime = DateTimeOffset.UtcNow;
            return result;
        }

        foreach (var repair in repairsList)
        {
            try
            {
                var success = await RevertSingleRepairAsync(repair, force, staffId, dryRun);
                if (success)
                {
                    result.SuccessfulRepairs++;
                    result.SuccessfulRepairDetails.Add(new RepairDetail
                    {
                        RuleId = repair.RuleId,
                        EntityType = repair.EntityType,
                        EntityId = repair.EntityId,
                        PropertyCode = repair.PropertyCode,
                        OldValue = repair.CurrentValue, // Old value before repair
                        NewValue = repair.ProposedValue, // Value that was set during repair
                        Action = "revert:restore"
                    });
                }
                else
                {
                    result.FailedRepairs++;
                    result.FailedRepairDetails.Add(new RepairFailure
                    {
                        RuleId = repair.RuleId,
                        EntityType = repair.EntityType,
                        EntityId = repair.EntityId,
                        PropertyCode = repair.PropertyCode,
                        AttemptedValue = repair.CurrentValue,
                        ErrorMessage = "Revert operation failed"
                    });
                }
            }
            catch (Exception ex)
            {
                result.FailedRepairs++;
                result.FailedRepairDetails.Add(new RepairFailure
                {
                    RuleId = repair.RuleId,
                    EntityType = repair.EntityType,
                    EntityId = repair.EntityId,
                    PropertyCode = repair.PropertyCode,
                    AttemptedValue = repair.CurrentValue,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });

                _logger.LogError(ex, "Failed to revert {EntityType}#{EntityId}.{PropertyCode}",
                    repair.EntityType, repair.EntityId, repair.PropertyCode);
            }
        }

        result.EndTime = DateTimeOffset.UtcNow;

        _logger.LogInformation("Revert operation completed in {Duration}ms. Success: {SuccessCount}, Failed: {FailedCount}, Success Rate: {SuccessRate:F1}%",
            result.Duration.TotalMilliseconds, result.SuccessfulRepairs, result.FailedRepairs, result.SuccessRate);

        return result;
    }

    public async Task<RepairResult> RevertByFilterAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? ruleId = null,
        string? entityType = null,
        long? entityId = null,
        long? runId = null,
        bool force = false,
        int staffId = 1,
        bool dryRun = false)
    {
        _logger.LogInformation("Fetching repair events for revert (Rule: {RuleId}, EntityType: {EntityType}, EntityId: {EntityId}, RunId: {RunId})",
            ruleId, entityType, entityId, runId);

        var repairs = await _auditRepository.GetRepairEventsAsync(from, to, ruleId, entityType, entityId, runId);

        _logger.LogInformation("Found {RepairCount} repair events to revert", repairs.Count());

        return await RevertAsync(repairs, force, staffId, dryRun);
    }

    private async Task<bool> RevertSingleRepairAsync(AuditEntry repair, bool force, int staffId, bool dryRun)
    {
        _logger.LogDebug("Reverting {EntityType}#{EntityId}.{PropertyCode}: '{NewValue}' → '{OldValue}'",
            repair.EntityType, repair.EntityId, repair.PropertyCode, repair.ProposedValue, repair.CurrentValue);

        // Get current value to verify it matches what we expect
        var currentValue = await GetCurrentPropertyValueAsync(repair.EntityType, repair.EntityId, repair.PropertyCode);

        // Safety check: ensure current value matches the value that was set during repair
        if (!force && !string.Equals(currentValue, repair.ProposedValue, StringComparison.Ordinal))
        {
            _logger.LogWarning("Safety check failed for {EntityType}#{EntityId}.{PropertyCode}. Expected current value '{Expected}' but found '{Actual}'. Use --force to override.",
                repair.EntityType, repair.EntityId, repair.PropertyCode, repair.ProposedValue, currentValue);
            return false;
        }

        if (dryRun)
        {
            _logger.LogInformation("[DRY RUN] Would revert {EntityType}#{EntityId}.{PropertyCode} from '{Current}' to '{Target}'",
                repair.EntityType, repair.EntityId, repair.PropertyCode, currentValue, repair.CurrentValue);
            return true;
        }

        try
        {
            // If OldValue is null, we need to delete the property value
            if (string.IsNullOrEmpty(repair.CurrentValue))
            {
                await DeletePropertyValueAsync(repair.EntityType, repair.EntityId, repair.PropertyCode);
                _logger.LogDebug("Deleted property {EntityType}#{EntityId}.{PropertyCode}",
                    repair.EntityType, repair.EntityId, repair.PropertyCode);
            }
            else
            {
                // Otherwise, set it back to the old value
                await _extPropRepository.UpsertExtPropertyAsync(
                    repair.EntityType,
                    repair.EntityId,
                    repair.PropertyCode,
                    repair.CurrentValue,
                    staffId);

                _logger.LogDebug("Reverted {EntityType}#{EntityId}.{PropertyCode} to '{OldValue}'",
                    repair.EntityType, repair.EntityId, repair.PropertyCode, repair.CurrentValue);
            }

            // Log the revert operation (ensure datetime formatting when applicable)
            var formattedCurrent = await FormatDateForAuditIfNeededAsync(repair.EntityType, repair.PropertyCode, currentValue);
            var formattedOld = await FormatDateForAuditIfNeededAsync(repair.EntityType, repair.PropertyCode, repair.CurrentValue);

            var revertAudit = new AuditEntry(
                repair.RuleId,
                repair.EntityType,
                repair.EntityId,
                repair.PropertyCode,
                formattedCurrent, // Current value before revert (formatted if datetime)
                formattedOld,     // Target value (formatted if datetime)
                "revert:restore",
                "info"
            );

            await _auditRepository.WriteAuditAsync(revertAudit, staffId,
                $"Reverted repair from audit entry. Original repair set '{repair.ProposedValue}'");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revert {EntityType}#{EntityId}.{PropertyCode}",
                repair.EntityType, repair.EntityId, repair.PropertyCode);
            return false;
        }
    }

    private async Task<string?> GetCurrentPropertyValueAsync(string entityType, long entityId, string propertyCode)
    {
        // This is a simplified implementation - in a real scenario you'd need to add this method to IExtPropRepository
        // For now, we'll use a basic approach
        try
        {
            if (entityType == "Document")
            {
                var documents = await _extPropRepository.GetDocumentsAsync();
                var document = documents.FirstOrDefault(e => e.EntityId == entityId);
                if (document?.Ext.TryGetValue(propertyCode, out var value) == true)
                {
                    return value;
                }
            }
            else if (entityType == "Student")
            {
                var students = await _extPropRepository.GetStudentsAsync();
                var student = students.FirstOrDefault(e => e.EntityId == entityId);
                if (student?.Ext.TryGetValue(propertyCode, out var value) == true)
                {
                    return value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current value for {EntityType}#{EntityId}.{PropertyCode}",
                entityType, entityId, propertyCode);
        }

        return null;
    }

    private async Task DeletePropertyValueAsync(string entityType, long entityId, string propertyCode)
    {
        // This is a simplified implementation - in a real scenario you'd need to add this method to IExtPropRepository
        // For now, we'll set it to null which should work with our UpsertExtPropertyAsync
        await _extPropRepository.UpsertExtPropertyAsync(entityType, entityId, propertyCode, null, 1);
    }

    private async Task<string?> FormatDateForAuditIfNeededAsync(string entityType, string propertyCode, string? input)
    {
        // If input is null/blank, return null
        if (string.IsNullOrWhiteSpace(input))
            return null;

        bool isDateType = false;
        try
        {
            var defs = await _extPropRepository.GetPropertyDefinitionsAsync(entityType);
            var def = defs.FirstOrDefault(d => string.Equals(d.PropertyCode, propertyCode, StringComparison.OrdinalIgnoreCase));
            if (def != null)
            {
                // DataType maps to PropertyType column in definition query
                isDateType = string.Equals(def.DataType, "datetime", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // If lookup fails, default to not formatting
            isDateType = false;
        }

        if (!isDateType)
            return input;

        // Try to parse and normalize to midnight, then format as yyyy/MM/dd HH:mm:ss
        if (DateTime.TryParse(input.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            var normalized = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, 0, DateTimeKind.Unspecified);
            return normalized.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        // If parse fails, return original string (avoid losing data)
        return input;
    }
}
