using System.Text.RegularExpressions;

namespace AcadSync.Processor;

public static class Evaluator
{
    public static IEnumerable<Violation> Evaluate(EprlDoc doc, IEnumerable<IEntityProjection> entities)
    {
        var defaults = doc.Defaults ?? new Defaults();
        foreach (var rule in doc.Rules)
        {
            foreach (var entity in entities.Where(e => e.EntityType.Equals(rule.Scope.Entity, StringComparison.OrdinalIgnoreCase)))
            {
                if (!Matches(rule.When, entity)) continue;

                var mode = rule.Mode ?? defaults.Mode ?? EprlMode.validate;
                var ruleViolations = new List<Violation>();

                foreach (var req in rule.Requirements)
                {
                    var current = entity.Ext.TryGetValue(req.property, out var v) ? v : null;

                    // Apply normalization preview on current (without writing)
                    var normalizedCurrent = Normalize(current, req.normalize, req.type);

                    var (ok, reason) = Check(req, normalizedCurrent);
                    if (ok) continue;

                    // Try to derive a value if we are allowed to "repair"
                    string? proposed = normalizedCurrent;

                    if ((mode == EprlMode.repair || mode == EprlMode.simulate) && req.source?.@try != null)
                    {
                        foreach (var step in req.source.@try)
                        {
                            object? val = null;
                            if (step.value is not null) val = step.value;
                            else if (!string.IsNullOrWhiteSpace(step.path)) val = entity.ResolvePath(step.path);
                            // step.sql/expression intentionally skipped in tiny evaluator (you can plug repo later)

                            var str = CoerceToString(val, req.type, req.normalize);
                            var norm = Normalize(str, req.normalize, req.type);
                            var (ok2, _) = Check(req, norm);
                            if (ok2)
                            {
                                proposed = norm;
                                break;
                            }
                        }
                    }

                    var sev = req.onFailure?.severity
                              ?? rule.OnGroupFailure?.severity
                              ?? (doc.Defaults?.Severity ?? Severity.error);

                    var action = (mode == EprlMode.repair || mode == EprlMode.simulate)
                                 ? (req.onFailure?.actions?.FirstOrDefault(a => a.StartsWith("repair:")) ?? "repair:upsert")
                                 : "none";

                    ruleViolations.Add(new Violation(
                        rule.Id, entity.EntityType, entity.EntityId, req.property,
                        current, proposed, reason, sev, action
                    ));
                }

                foreach (var v in ruleViolations) yield return v;
            }
        }
    }

    // ---------- helpers ----------
    private static bool Matches(Condition? c, IEntityProjection entity)
        => c == null || Eval(c);

    private static bool Eval(Condition c)
    {
        if (c.all != null) return c.all.All(Eval);
        if (c.any != null) return c.any.Any(Eval);
        if (c.none != null) return !c.none.Any(Eval);

        var ent = _currentEntity ?? throw new InvalidOperationException("No entity in context");
        bool CmpVal(object? left, object? right, Func<int, bool> cmp)
        {
            var (l, r) = Promote(left, right);
            int comp = Comparer<object>.Default.Compare(l, r);
            return cmp(comp);
        }

        if (c.eq != null) return Equals(Resolve(c.eq.path), c.eq.value?.ToString());
        if (c.ne != null) return !Equals(Resolve(c.ne.path), c.ne.value?.ToString());
        if (c.@in != null) return c.@in.values.Select(v => v?.ToString()).Contains(Resolve(c.@in.path));
        if (c.notIn != null) return !c.notIn.values.Select(v => v?.ToString()).Contains(Resolve(c.notIn.path));
        if (c.regex != null) return Regex.IsMatch(Resolve(c.regex.path) ?? "", c.regex.value?.ToString() ?? "");
        if (c.gt != null) return CmpVal(Resolve(c.gt.path), c.gt.value, i => i > 0);
        if (c.gte != null) return CmpVal(Resolve(c.gte.path), c.gte.value, i => i >= 0);
        if (c.lt != null) return CmpVal(Resolve(c.lt.path), c.lt.value, i => i < 0);
        if (c.lte != null) return CmpVal(Resolve(c.lte.path), c.lte.value, i => i <= 0);
        if (c.exists != null) return Resolve(c.exists.path) is not null;
        if (c.notExists != null) return Resolve(c.notExists.path) is null;

        return true;

        string? Resolve(string path) => ent.ResolvePath(path)?.ToString();

        static (object, object) Promote(object? left, object? right)
        {
            if (decimal.TryParse(left?.ToString(), out var dl) && decimal.TryParse(right?.ToString(), out var dr))
                return (dl, dr);
            if (DateTimeOffset.TryParse(left?.ToString(), out var tl) && DateTimeOffset.TryParse(right?.ToString(), out var tr))
                return (tl, tr);
            return (left?.ToString() ?? "", right?.ToString() ?? "");
        }
    }

    [ThreadStatic] private static IEntityProjection? _currentEntity;

    // Wrap per-entity to allow condition resolution to access current entity.
    public static IEnumerable<Violation> EvaluateWithContext(EprlDoc doc, IEnumerable<IEntityProjection> entities)
    {
        foreach (var e in entities)
        {
            _currentEntity = e;
            foreach (var v in Evaluate(doc, new[] { e })) yield return v;
        }
        _currentEntity = null;
    }

    private static (bool ok, string reason) Check(Requirement req, string? value)
    {
        // Required check
        if (req.required && string.IsNullOrWhiteSpace(value))
            return (false, "required");

        if (string.IsNullOrWhiteSpace(value))
            return (true, "empty-ok");

        var c = req.constraints;
        if (c is null) return (true, "ok");

        // regex
        if (!string.IsNullOrEmpty(c.regex) && !Regex.IsMatch(value!, c.regex))
            return (false, $"regex({c.regex})");

        // eq/neq
        if (c.eq is not null && !value!.Equals(c.eq.ToString(), StringComparison.OrdinalIgnoreCase))
            return (false, $"eq({c.eq})");
        if (c.neq is not null && value!.Equals(c.neq.ToString(), StringComparison.OrdinalIgnoreCase))
            return (false, $"neq({c.neq})");

        // in/anyOf/noneOf
        if (c.@in is not null && !c.@in.Select(v => v?.ToString()).Contains(value)) return (false, "in");
        if (c.anyOf is not null && !c.anyOf.Select(v => v?.ToString()).Contains(value)) return (false, "anyOf");
        if (c.noneOf is not null && c.noneOf.Select(v => v?.ToString()).Contains(value)) return (false, "noneOf");

        // numeric range
        if ((c.min is not null || c.max is not null) && decimal.TryParse(value, out var dec))
        {
            if (c.min is not null && dec < c.min) return (false, $"min({c.min})");
            if (c.max is not null && dec > c.max) return (false, $"max({c.max})");
        }

        // length
        if (c.minLen is not null && value!.Length < c.minLen) return (false, $"minLen({c.minLen})");
        if (c.maxLen is not null && value!.Length > c.maxLen) return (false, $"maxLen({c.maxLen})");

        // gte/lte dates or numbers
        if (c.gte is not null && !MeetsBound(value, c.gte, lower: true)) return (false, $"gte({c.gte})");
        if (c.lte is not null && !MeetsBound(value, c.lte, lower: false)) return (false, $"lte({c.lte})");

        return (true, "ok");
    }

    private static bool MeetsBound(string value, string bound, bool lower)
    {
        if (decimal.TryParse(value, out var dv) && decimal.TryParse(bound, out var db))
            return lower ? dv >= db : dv <= db;

        if (DateTimeOffset.TryParse(value, out var tv) && DateTimeOffset.TryParse(bound, out var tb))
            return lower ? tv >= tb : tv <= tb;

        // string compare
        int cmp = string.Compare(value, bound, StringComparison.Ordinal);
        return lower ? cmp >= 0 : cmp <= 0;
    }

    private static string? Normalize(string? value, Normalization? n, Models.Domain.ValueType type)
    {
        if (value is null) return null;
        var s = value;

        if (n?.trim == true) s = s.Trim();
        if (n?.upper == true) s = s.ToUpperInvariant();
        if (n?.padLeft is not null && s.Length < n.padLeft.length)
        {
            var padChar = (n.padLeft.padChar ?? "0")[0];
            s = s.PadLeft(n.padLeft.length, padChar);
        }
        if (type == Models.Domain.ValueType.@bool && n?.mapBool is not null)
        {
            if (n.mapBool.truthy?.Contains(s, StringComparer.OrdinalIgnoreCase) == true) s = "true";
            else if (n.mapBool.falsy?.Contains(s, StringComparer.OrdinalIgnoreCase) == true) s = "false";
        }
        if (type == Models.Domain.ValueType.@date && !string.IsNullOrWhiteSpace(n?.dateFormat))
        {
            if (DateTimeOffset.TryParse(s, out var dt))
                s = dt.ToString("yyyy-MM-dd");
        }
        return s;
    }

    private static string? CoerceToString(object? val, Models.Domain.ValueType type, Normalization? n)
    {
        if (val is null) return null;
        return type switch
        {
            Models.Domain.ValueType.@date when DateTimeOffset.TryParse(val.ToString(), out var dt) => dt.ToString("yyyy-MM-dd"),
            Models.Domain.ValueType.@bool => (val is bool b ? b : new[] { "true", "1", "y", "yes" }.Contains(val.ToString()!.ToLowerInvariant())).ToString().ToLowerInvariant(),
            _ => val.ToString()
        };
    }
}
