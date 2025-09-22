using System;
using System.Collections.Generic;

namespace AcadSync.Web.Models
{
    public class WebRun
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Mode { get; set; } = null!; // e.g. demo, validate, simulate, repair, revert
        public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }
        public int ViolationsFound { get; set; }
        public int ViolationsProcessed { get; set; }
        public string? Notes { get; set; }

        // Navigation
        public List<RunLog> Logs { get; set; } = new();
    }
}
