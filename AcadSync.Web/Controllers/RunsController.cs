using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AcadSync.Web.Services;
using AcadSync.Web.Models;
using AcadSync.Web.Data;
using Microsoft.AspNetCore.Authorization;

namespace AcadSync.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RunsController : ControllerBase
    {
        private readonly RunManager _runManager;

        public RunsController(RunManager runManager)
        {
            _runManager = runManager;
        }

        public class RunRequestDto
        {
            public string Mode { get; set; } = null!;
            public long? TargetRunId { get; set; }
            public DateTimeOffset? FromDate { get; set; }
            public bool Force { get; set; } = false;
            public bool DryRun { get; set; } = false;
            public int? StaffId { get; set; }
            public string? Notes { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> StartRun([FromBody] RunRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Mode))
                return BadRequest("Mode is required");

            var request = new RunRequest
            {
                Mode = dto.Mode,
                TargetRunId = dto.TargetRunId,
                FromDate = dto.FromDate,
                Force = dto.Force,
                DryRun = dto.DryRun,
                StaffId = dto.StaffId,
                Notes = dto.Notes
            };

            var runId = await _runManager.EnqueueAsync(request);
            return Accepted(new { runId });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetRun(Guid id)
        {
            // fetch run metadata from DB
            using var scope = HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AcadSyncWebDbContext>();
            var run = await db.WebRuns.FindAsync(id);
            if (run == null) return NotFound();
            return Ok(run);
        }
    }
}
