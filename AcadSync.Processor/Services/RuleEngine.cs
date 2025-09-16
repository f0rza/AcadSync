using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using AcadSync.Processor.Interfaces;
using AcadSync.Processor.Models.Domain;
using AcadSync.Processor.Models.Projections;

namespace AcadSync.Processor.Services;

/// <summary>
/// Non-static, injectable rule evaluation engine
/// </summary>
public class RuleEngine : IRuleEngine
{
    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(ILogger<RuleEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<Violation>> EvaluateAsync(EprlDoc doc, IEnumerable<IEntityProjection> entities)
    {
        _logger.LogDebug("Starting rule evaluation for {EntityCount} entities with {RuleCount} rules", 
            entities.Count(), doc.Rules.Count);

        var violations = new List<Violation>();
        var defaults = doc.Defaults ?? new Defaults();

        foreach (var entity in entities)
        {
            var entityViolations = await EvaluateEntityAsync(doc, entity);
            violations.AddRange(entityViolations);
        }

        _logger.LogDebug("Rule evaluation completed. Found {ViolationCount} violations", violations.Count);
        return violations;
    }

    public async Task<IEnumerable<Violation>> EvaluateEntityAsync(EprlDoc doc, IEntityProjection entity)
    {
        var violations = new List<Violation>();
        var defaults = doc.Defaults ?? new Defaults();

        foreach (var rule in doc.Rules)
        {
            // Check if rule applies to this entity type
            if (!entity.EntityType.Equals(rule.Scope.Entity, StringComparison.OrdinalIgnoreCase))
                continue;

            // Check if entity matches rule conditions
            if (!await MatchesConditionAsync(rule.When, entity))
                continue;

            var mode = rule.Mode ?? defaults.Mode ?? EprlMode.validate;
            var ruleViolations = await EvaluateRuleAsync(rule, entity, mode, defaults);
            violations.AddRange(ruleViolations);
        }

        return violations;
    }

    private async Task<List<Violation>> EvaluateRuleAsync(Rule rule, IEntityProjection entity, EprlMode mode, Defaults defaults)
    {
        var violations = new List<Violation>();

        foreach (var req in rule.Requirements)
        {
            var current = entity.Ext.TryGetValue(req.property, out var v) ? v : null;

            // Apply normalization preview on current (without writing)
            var normalizedCurrent = await NormalizeAsync(current, req.normalize, req.type);

            var (ok, reason) = await CheckRequirementAsync(req, normalizedCurrent);
            if (ok) continue;

            // Try to derive a value if we are allowed to "repair"
            string? proposed = normalizedCurrent;

            if ((mode == EprlMode.repair || mode == EprlMode.simulate) && req.source?.@try != null)
            {
                proposed = await TryDeriveValueAsync(req, entity);
            }

            var severity = req.onFailure?.severity
                          ?? rule.OnGroupFailure?.severity
                          ?? (defaults.Severity ?? Severity.error);

            var action = (mode == EprlMode.repair || mode == EprlMode.simulate)
                         ? (req.onFailure?.actions?.FirstOrDefault(a => a.StartsWith("repair:")) ?? "repair:upsert")
                         : "none";

            violations.Add(new Violation(
                rule.Id, entity.EntityType, entity.EntityId, req.property,
                current, proposed, reason, severity, action
            ));
        }

        return violations;
    }

    private Task<bool> MatchesConditionAsync(Condition? condition, IEntityProjection entity)
    {
        if (condition == null) return Task.FromResult(true);

        return EvaluateConditionAsync(condition, entity);
    }

    private async Task<bool> EvaluateConditionAsync(Condition condition, IEntityProjection entity)
    {
        // Handle logical operators
        if (condition.all != null)
        {
            foreach (var subCondition in condition.all)
            {
                if (!await EvaluateConditionAsync(subCondition, entity))
                    return false;
            }
            return true;
        }

        if (condition.any != null)
        {
            foreach (var subCondition in condition.any)
            {
                if (await EvaluateConditionAsync(subCondition, entity))
                    return true;
            }
            return false;
        }

        if (condition.none != null)
        {
            foreach (var subCondition in condition.none)
            {
                if (await EvaluateConditionAsync(subCondition, entity))
                    return false;
            }
            return true;
        }

        // Handle comparison operators
        bool CmpVal(object? left, object? right, Func<int, bool> cmp)
        {
            var (l, r) = PromoteValues(left, right);
            int comp = Comparer<object>.Default.Compare(l, r);
            return cmp(comp);
        }

        string? ResolveValue(string path) => entity.ResolvePath(path)?.ToString();

        if (condition.eq != null) return Equals(ResolveValue(condition.eq.path), condition.eq.value?.ToString());
        if (condition.ne != null) return !Equals(ResolveValue(condition.ne.path), condition.ne.value?.ToString());
        if (condition.@in != null) return condition.@in.values.Select(v => v?.ToString()).Contains(ResolveValue(condition.@in.path));
        if (condition.notIn != null) return !condition.notIn.values.Select(v => v?.ToString()).Contains(ResolveValue(condition.notIn.path));
        if (condition.regex != null) return Regex.IsMatch(ResolveValue(condition.regex.path) ?? "", condition.regex.value?.ToString() ?? "");
        if (condition.gt != null) return CmpVal(ResolveValue(condition.gt.path), condition.gt.value, i => i > 0);
        if (condition.gte != null) return CmpVal(ResolveValue(condition.gte.path), condition.gte.value, i => i >= 0);
        if (condition.lt != null) return CmpVal(ResolveValue(condition.lt.path), condition.lt.value, i => i < 0);
        if (condition.lte != null) return CmpVal(ResolveValue(condition.lte.path), condition.lte.value, i => i <= 0);
        if (condition.exists != null) return ResolveValue(condition.exists.path) is not null;
        if (condition.notExists != null) return ResolveValue(condition.notExists.path) is null;

        return true;
    }

    private async Task<(bool ok, string reason)> CheckRequirementAsync(Requirement req, string? value)
    {
        // Required check
        if (req.required && string.IsNullOrWhiteSpace(value))
            return await Task.FromResult((false, "required"));

        if (string.IsNullOrWhiteSpace(value))
            return await Task.FromResult((true, "empty-ok"));

        var c = req.constraints;
        if (c is null) return await Task.FromResult((true, "ok"));

        // regex
        if (!string.IsNullOrEmpty(c.regex) && !Regex.IsMatch(value!, c.regex))
            return await Task.FromResult((false, $"regex({c.regex})"));

        // eq/neq
        if (c.eq is not null && !value!.Equals(c.eq.ToString(), StringComparison.OrdinalIgnoreCase))
            return await Task.FromResult((false, $"eq({c.eq})"));
        if (c.neq is not null && value!.Equals(c.neq.ToString(), StringComparison.OrdinalIgnoreCase))
            return await Task.FromResult((false, $"neq({c.neq})"));

        // in/anyOf/noneOf
        if (c.@in is not null && !c.@in.Select(v => v?.ToString()).Contains(value)) return await Task.FromResult((false, "in"));
        if (c.anyOf is not null && !c.anyOf.Select(v => v?.ToString()).Contains(value)) return await Task.FromResult((false, "anyOf"));
        if (c.noneOf is not null && c.noneOf.Select(v => v?.ToString()).Contains(value)) return await Task.FromResult((false, "noneOf"));

        // numeric range
        if ((c.min is not null || c.max is not null) && decimal.TryParse(value, out var dec))
        {
            if (c.min is not null && dec < c.min) return await Task.FromResult((false, $"min({c.min})"));
            if (c.max is not null && dec > c.max) return await Task.FromResult((false, $"max({c.max})"));
        }

        // length
        if (c.minLen is not null && value!.Length < c.minLen) return await Task.FromResult((false, $"minLen({c.minLen})"));
        if (c.maxLen is not null && value!.Length > c.maxLen) return await Task.FromResult((false, $"maxLen({c.maxLen})"));

        // gte/lte dates or numbers
        if (c.gte is not null && !MeetsBound(value, c.gte, lower: true)) return await Task.FromResult((false, $"gte({c.gte})"));
        if (c.lte is not null && !MeetsBound(value, c.lte, lower: false)) return await Task.FromResult((false, $"lte({c.lte})"));

        return await Task.FromResult((true, "ok"));
    }

    private async Task<string?> TryDeriveValueAsync(Requirement req, IEntityProjection entity)
    {
        if (req.source?.@try == null) return null;

        foreach (var step in req.source.@try)
        {
            object? val = null;
            if (step.value is not null) val = step.value;
            else if (!string.IsNullOrWhiteSpace(step.path)) val = entity.ResolvePath(step.path);
            // step.sql/expression intentionally skipped for now

            var str = CoerceToString(val, req.type, req.normalize);
            var norm = await NormalizeAsync(str, req.normalize, req.type);
            var (ok, _) = await CheckRequirementAsync(req, norm);
            if (ok)
            {
                return norm;
            }
        }

        return null;
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

    private async Task<string?> NormalizeAsync(string? value, Normalization? n, Models.Domain.ValueType type)
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
        return await Task.FromResult(s);
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

    private static (object, object) PromoteValues(object? left, object? right)
    {
        if (decimal.TryParse(left?.ToString(), out var dl) && decimal.TryParse(right?.ToString(), out var dr))
            return (dl, dr);
        if (DateTimeOffset.TryParse(left?.ToString(), out var tl) && DateTimeOffset.TryParse(right?.ToString(), out var tr))
            return (tl, tr);
        return (left?.ToString() ?? "", right?.ToString() ?? "");
    }
}
