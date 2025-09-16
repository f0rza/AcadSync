namespace AcadSync.Processor.Models.Projections;

using AcadSync.Processor.Utilities;

public sealed record StudentProjection(
    long id,
    string studentNumber,
    string? programCode,
    string? status,
    string? campus,
    string? citizenship,
    string? visaType,
    string? country,
    List<DocumentItem> documents,
    Dictionary<string, string?> ext
) : IEntityProjection
{
    public string EntityType => "Student";
    public long EntityId => id;
    public Dictionary<string, string?> Ext => ext;

    public object? ResolvePath(string path)
        => PathResolver.Resolve(this, path);
}
