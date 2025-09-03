namespace AcadSync.Processor;

public enum EprlMode { validate, repair, simulate }
public enum Severity { info, warning, error, block }
public enum ValueType { @string, @int, @decimal, @date, @bool, lookup }

public class EprlDoc
{
    public string ApiVersion { get; set; } = "";
    public string Tenant { get; set; } = "";
    public RuleSet Ruleset { get; set; } = new();
    public Defaults? Defaults { get; set; }
    public List<Rule> Rules { get; set; } = new();
}

public class RuleSet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string? EffectiveFrom { get; set; }
    public string? Owner { get; set; }
}

public class Defaults
{
    public EprlMode? Mode { get; set; }
    public Severity? Severity { get; set; }
    public string? Timezone { get; set; }
}

public sealed record Scope(string entity); // Student|Course|Section|Document|Group|Enrollment|Account|Program

public sealed record Compare(string path, object? value = null);
public sealed record MultiCompare(string path, List<object> values);
public sealed record PathOnly(string path);

public sealed record Lookup(List<string> list);
public sealed record Normalization(
    bool? upper = null,
    bool? trim = null,
    PadLeft? padLeft = null,
    MapBool? mapBool = null,
    string? dateFormat = null   // normalize incoming date text -> ISO yyyy-MM-dd
);
public sealed record PadLeft(int length, string padChar = "0");
public sealed record MapBool(List<string>? truthy = null, List<string>? falsy = null);

public sealed record Source(List<SourceStep> @try);
public sealed record SourceStep(string? path = null, string? sql = null, object? value = null, string? expression = null);

public sealed record ActionsBlock(Severity? severity = null, List<string>? actions = null);
