namespace AcadSync.Processor;

// ---- Conditions (composable) ----
public sealed record Condition(
    List<Condition>? all = null,
    List<Condition>? any = null,
    List<Condition>? none = null,
    Compare? eq = null,
    Compare? ne = null,
    MultiCompare? @in = null,
    MultiCompare? notIn = null,
    Compare? regex = null,
    Compare? gt = null,
    Compare? gte = null,
    Compare? lt = null,
    Compare? lte = null,
    PathOnly? exists = null,
    PathOnly? notExists = null
);
