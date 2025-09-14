namespace AcadSync.Processor.Models.Domain;

public class Rule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Scope Scope { get; set; } = new();
    public List<Requirement> Requirements { get; set; } = new();
    public Condition? When { get; set; }
    public EprlMode? Mode { get; set; }
    public ActionsBlock? OnGroupFailure { get; set; }
}
