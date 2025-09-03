namespace AcadSync.Processor;

// ------------------ Path resolver (very small, dot + simple filters) ------------------
public static class PathResolver
{
    // Supports: "programCode", "documents[DocType=IMM].fields.ExpiryDate", "meta.SomeKey", "ext.SomeProp"
    public static object? Resolve(object root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        object? current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current is null) return null;

            // Handle [Key=Value] filter on lists
            string seg = segment;
            string? filterKey = null, filterVal = null;
            int bracket = segment.IndexOf('[');
            if (bracket >= 0 && segment.EndsWith("]"))
            {
                seg = segment[..bracket];
                var filter = segment[(bracket + 1)..^1]; // Key=Value
                var parts = filter.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2) { filterKey = parts[0]; filterVal = parts[1].Trim('"'); }
            }

            current = seg switch
            {
                "ext" when current is IEntityProjection p => p.Ext,
                _ => GetMember(current, seg)
            };

            if (filterKey != null && current is System.Collections.IEnumerable en)
            {
                foreach (var item in en)
                {
                    var candidate = GetMember(item!, filterKey);
                    if (candidate?.ToString()?.Equals(filterVal, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        current = item;
                        break;
                    }
                    current = null;
                }
            }
        }
        return current;
    }

    private static object? GetMember(object obj, string name)
    {
        var t = obj.GetType();
        // Dictionary<string,?>
        if (obj is System.Collections.IDictionary dict)
            return dict[name];

        // Try property
        var p = t.GetProperty(name);
        if (p != null) return p.GetValue(obj);

        // Try field
        var f = t.GetField(name);
        if (f != null) return f.GetValue(obj);

        return null;
    }
}
