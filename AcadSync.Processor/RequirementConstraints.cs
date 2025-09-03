namespace AcadSync.Processor;

public sealed record RequirementConstraints(
    string? regex = null,
    object? eq = null,
    object? neq = null,
    List<object>? @in = null,
    List<object>? anyOf = null,
    List<object>? noneOf = null,
    decimal? min = null,
    decimal? max = null,
    int? minLen = null,
    int? maxLen = null,
    string? gte = null,     // numbers or ISO date
    string? lte = null
);
