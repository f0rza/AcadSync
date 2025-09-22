using System;

namespace AcadSync.Web.Models
{
    public class RunLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RunId { get; set; }
        public WebRun? Run { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Level { get; set; } = "Information";
        public string Message { get; set; } = null!;
        public string? Details { get; set; }
    }
}
