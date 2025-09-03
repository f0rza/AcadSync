namespace AcadSync.Processor;

public sealed record DocumentProjection(
    long id,
    string documentTypeCode,
    Dictionary<string, object?> meta,
    Dictionary<string, string?> ext
) : IEntityProjection
{
    public string EntityType => "Document";
    public long EntityId => id;
    public Dictionary<string, string?> Ext => ext;

    public object? ResolvePath(string path)
        => PathResolver.Resolve(this, path);
}
