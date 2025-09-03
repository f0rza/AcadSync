using AcadSync.Audit.Models;

namespace AcadSync.Audit.Extensions;

/// <summary>
/// Extension methods for converting violations to audit entries
/// </summary>
public static class ViolationExtensions
{
    /// <summary>
    /// Convert a Violation to an AuditEntry using reflection to avoid direct dependency
    /// </summary>
    public static AuditEntry ToAuditEntry(this object violation)
    {
        // Use reflection to extract properties from the violation object
        var violationType = violation.GetType();
        
        var ruleId = violationType.GetProperty("RuleId")?.GetValue(violation)?.ToString() ?? "";
        var entityType = violationType.GetProperty("EntityType")?.GetValue(violation)?.ToString() ?? "";
        var entityId = (long)(violationType.GetProperty("EntityId")?.GetValue(violation) ?? 0L);
        var propertyCode = violationType.GetProperty("PropertyCode")?.GetValue(violation)?.ToString() ?? "";
        var currentValue = violationType.GetProperty("CurrentValue")?.GetValue(violation)?.ToString();
        var proposedValue = violationType.GetProperty("ProposedValue")?.GetValue(violation)?.ToString();
        var action = violationType.GetProperty("Action")?.GetValue(violation)?.ToString() ?? "";
        var severity = violationType.GetProperty("Severity")?.GetValue(violation)?.ToString() ?? "";

        return new AuditEntry(
            ruleId,
            entityType,
            entityId,
            propertyCode,
            currentValue,
            proposedValue,
            action,
            severity
        );
    }
}
