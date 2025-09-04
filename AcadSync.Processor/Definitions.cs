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

public sealed record Scope
{
    public string Entity { get; init; } = "";
}; // Student|Course|Section|Document|Group|Enrollment|Account|Program

public sealed record Compare
{
    public string path { get; init; } = string.Empty;
    public object? value { get; init; }
}

public sealed record MultiCompare
{
    public string path { get; init; } = string.Empty;
    public List<object> values { get; init; } = new();
}

public sealed record PathOnly
{
    public string path { get; init; } = string.Empty;
}

public sealed record Lookup
{
    public List<string> list { get; init; } = new();
}

public sealed record Normalization
{
    public bool? upper { get; init; }
    public bool? trim { get; init; }
    public PadLeft? padLeft { get; init; }
    public MapBool? mapBool { get; init; }
    public string? dateFormat { get; init; }   // normalize incoming date text -> ISO yyyy-MM-dd
}

public sealed record PadLeft
{
    public int length { get; init; }
    public string padChar { get; init; } = "0";
}

public sealed record MapBool
{
    public List<string>? truthy { get; init; }
    public List<string>? falsy { get; init; }
}

public sealed record Source
{
    public List<SourceStep> @try { get; init; } = new();
}

public sealed record SourceStep
{
    public string? path { get; init; }
    public string? sql { get; init; }
    public object? value { get; init; }
    public string? expression { get; init; }
}

public sealed record ActionsBlock
{
    public Severity? severity { get; init; }
    public List<string>? actions { get; init; }
}
