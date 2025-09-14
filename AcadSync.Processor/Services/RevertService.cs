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
        result.TotalViolationsAttempted = repairsList.Count;

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

        // In dry-run mode, skip safety check and only report what would be done
        if (dryRun)
        {
            _logger.LogInformation("[DRY RUN] Would revert {EntityType}#{EntityId}.{PropertyCode} from '{Current}' to '{Target}'",
                repair.EntityType, repair.EntityId, repair.PropertyCode, currentValue, repair.CurrentValue);
            return true;
        }

        // Safety check: ensure current value matches the value that was set during repair
        if (!force && !await ValuesMatchForRevertAsync(currentValue, repair.ProposedValue, repair.EntityType, repair.PropertyCode))
        {
            _logger.LogWarning("Safety check failed for {EntityType}#{EntityId}.{PropertyCode}. Expected current value '{Expected}' but found '{Actual}'. Use --force to override.",
                repair.EntityType, repair.EntityId, repair.PropertyCode, repair.ProposedValue, currentValue);
            return false;
        }

        try
        {
            bool writeSuccess = false;

            if (string.IsNullOrEmpty(repair.CurrentValue))
            {
                // Delete path for null target
                if (dryRun)
                {
                    _logger.LogInformation("[DRY RUN] Would delete property {EntityType}#{EntityId}.{PropertyCode}",
                        repair.EntityType, repair.EntityId, repair.PropertyCode);
                    writeSuccess = true;
                }
                else
                {
                    writeSuccess = await _extPropRepository.DeleteExtPropertyAsync(repair.EntityType, repair.EntityId, repair.PropertyCode);
                    if (!writeSuccess)
                        _logger.LogWarning("Delete did not remove any row for {EntityType}#{EntityId}.{PropertyCode}", repair.EntityType, repair.EntityId, repair.PropertyCode);
                }
            }
            else
            {
                // Upsert path for non-null target
                if (dryRun)
                {
                    _logger.LogInformation("[DRY RUN] Would upsert {EntityType}#{EntityId}.{PropertyCode} => '{Target}'",
                        repair.EntityType, repair.EntityId, repair.PropertyCode, repair.CurrentValue);
                    writeSuccess = true;
                }
                    else
                {
                    await _extPropRepository.UpsertExtPropertyAsync(repair.EntityType, repair.EntityId, repair.PropertyCode, repair.CurrentValue, staffId);

                    // Re-read to verify the write took effect
                    var afterValue = await GetCurrentPropertyValueAsync(repair.EntityType, repair.EntityId, repair.PropertyCode);
                    writeSuccess = await ValuesMatchForRevertAsync(afterValue, repair.CurrentValue, repair.EntityType, repair.PropertyCode);
                    if (!writeSuccess)
                    {
                        _logger.LogWarning("Post-write verification failed for {EntityType}#{EntityId}.{PropertyCode}. Expected '{Expected}' but found '{Actual}'",
                            repair.EntityType, repair.EntityId, repair.PropertyCode, repair.CurrentValue, afterValue);
                    }
                }
            }

            if (!writeSuccess)
            {
                _logger.LogWarning("Revert write failed for {EntityType}#{EntityId}.{PropertyCode}", repair.EntityType, repair.EntityId, repair.PropertyCode);
                return false;
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
        try
        {
            // Use repository's live read (will normalize entity name internally)
            return await _extPropRepository.GetCurrentPropertyValueAsync(entityType, entityId, propertyCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current value for {EntityType}#{EntityId}.{PropertyCode}",
                entityType, entityId, propertyCode);
            return null;
        }
    }

    private async Task DeletePropertyValueAsync(string entityType, long entityId, string propertyCode)
    {
        // This is a simplified implementation - in a real scenario you'd need to add this method to IExtPropRepository
        // For now, we'll set it to null which should work with our UpsertExtPropertyAsync
        await _extPropRepository.UpsertExtPropertyAsync(entityType, entityId, propertyCode, null, 1);
    }

    private async Task<bool> ValuesMatchForRevertAsync(string? currentValue, string? expectedValue, string entityType, string propertyCode)
    {
        // Handle null/empty cases
        if (string.IsNullOrWhiteSpace(currentValue) && string.IsNullOrWhiteSpace(expectedValue))
            return true;
        if (string.IsNullOrWhiteSpace(currentValue) || string.IsNullOrWhiteSpace(expectedValue))
            return false;

        // Check if this is a datetime property
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
            // If lookup fails, default to string comparison
            isDateType = false;
        }

        if (isDateType)
        {
            // For datetime properties, parse both values and compare as dates
            if (DateTime.TryParse(currentValue.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var currentDt) &&
                DateTime.TryParse(expectedValue.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var expectedDt))
            {
                // Compare dates chronologically (normalize to same precision)
                var currentNormalized = new DateTime(currentDt.Year, currentDt.Month, currentDt.Day, currentDt.Hour, currentDt.Minute, currentDt.Second, DateTimeKind.Unspecified);
                var expectedNormalized = new DateTime(expectedDt.Year, expectedDt.Month, expectedDt.Day, expectedDt.Hour, expectedDt.Minute, expectedDt.Second, DateTimeKind.Unspecified);

                return currentNormalized == expectedNormalized;
            }
        }

        // For non-datetime properties, use exact string comparison
        return string.Equals(currentValue, expectedValue, StringComparison.Ordinal);
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
