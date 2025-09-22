using System;
using System.Collections.Generic;

namespace AcadSync.Web.Models
{
    public class RuleEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? CurrentVersionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public byte[]? RowVersion { get; set; }

        // Navigation
        public List<RuleVersionEntity> Versions { get; set; } = new();
    }
}
