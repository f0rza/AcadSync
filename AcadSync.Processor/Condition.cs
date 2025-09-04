namespace AcadSync.Processor;

// ---- Conditions (composable) ----
public sealed record Condition
{
    public List<Condition>? all { get; init; }
    public List<Condition>? any { get; init; }
    public List<Condition>? none { get; init; }
    public Compare? eq { get; init; }
    public Compare? ne { get; init; }
    public MultiCompare? @in { get; init; }
    public MultiCompare? notIn { get; init; }
    public Compare? regex { get; init; }
    public Compare? gt { get; init; }
    public Compare? gte { get; init; }
    public Compare? lt { get; init; }
    public Compare? lte { get; init; }
    public PathOnly? exists { get; init; }
    public PathOnly? notExists { get; init; }
}
