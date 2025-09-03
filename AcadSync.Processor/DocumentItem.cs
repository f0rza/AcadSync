namespace AcadSync.Processor;

public sealed record DocumentItem(string docType, Dictionary<string, object?> fields);
