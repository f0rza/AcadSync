namespace AcadSync.Processor;

// ---- Requirements ----
public sealed record Requirement
{
    public string Property { get; init; } = "";
    public ValueType Type { get; init; } = ValueType.@string;
    public bool Required { get; init; } = false;
    public RequirementConstraints? Constraints { get; init; }
    public Lookup? Lookup { get; init; }
    public Normalization? Normalize { get; init; }
    public Source? Source { get; init; }
    public ActionsBlock? OnFailure { get; init; }
}
