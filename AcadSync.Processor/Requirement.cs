namespace AcadSync.Processor;

// ---- Requirements ----
public sealed record Requirement(
    string property,
    ValueType type,
    bool required,
    RequirementConstraints? constraints = null,
    Lookup? lookup = null,
    Normalization? normalize = null,
    Source? source = null,
    ActionsBlock? onFailure = null
);
