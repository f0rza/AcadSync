using System;

namespace AcadSync.Web.Models
{
    public class RuleVersionEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RuleId { get; set; }
        public RuleEntity? Rule { get; set; }

        // The YAML text of the rule version
        public string Yaml { get; set; } = null!;

        // Optional metadata
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
    }
}
