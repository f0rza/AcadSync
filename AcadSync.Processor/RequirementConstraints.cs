namespace AcadSync.Processor;

public sealed record RequirementConstraints
{
    public string? regex { get; init; }
    public object? eq { get; init; }
    public object? neq { get; init; }
    public List<object>? @in { get; init; }
    public List<object>? anyOf { get; init; }
    public List<object>? noneOf { get; init; }
    public decimal? min { get; init; }
    public decimal? max { get; init; }
    public int? minLen { get; init; }
    public int? maxLen { get; init; }
    public string? gte { get; init; }     // numbers or ISO date
    public string? lte { get; init; }
}
