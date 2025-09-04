namespace AcadSync.Processor;

// ---- Requirements ----
public sealed record Requirement
{
    public string property { get; init; } = string.Empty;
    public ValueType type { get; init; }
    public bool required { get; init; }
    public RequirementConstraints? constraints { get; init; }
    public Lookup? lookup { get; init; }
    public Normalization? normalize { get; init; }
    public Source? source { get; init; }
    public ActionsBlock? onFailure { get; init; }
}
