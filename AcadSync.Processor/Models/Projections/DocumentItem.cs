namespace AcadSync.Processor.Models.Projections;

public sealed record DocumentItem(string docType, Dictionary<string, object?> fields);
