namespace AcadSync.Processor;

// ------------------ Entity projections ------------------
// You can create separate projections per scope. Keep 'ext' as current ext-prop bag.
public interface IEntityProjection
{
    string EntityType { get; }
    long EntityId { get; }
    Dictionary<string, string?> Ext { get; }  // current ext-prop values (by code)
    object? ResolvePath(string path);
}
