namespace AcadSync.Processor.Models.Domain;

// ------------------ Evaluator ------------------

public sealed record Violation(
    string RuleId,
    string EntityType,
    long EntityId,
    string PropertyCode,
    string? CurrentValue,
    string? ProposedValue,
    string Reason,
    Severity Severity,
    string Action // "repair:upsert", "repair:normalize", "none", etc.
);
