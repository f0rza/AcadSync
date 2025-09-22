using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Linq;
using AcadSync.Web.Data;
using AcadSync.Web.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AcadSync.Web.Hubs;
using AcadSync.Processor.Services;
using AcadSync.Processor.Models.Results;
using AcadSync.Processor.Models.Domain;

namespace AcadSync.Web.Services
{
    /// <summary>
    /// Background run manager: queues run requests and executes them sequentially.
    /// Broadcasts progress/logs over SignalR and persists run metadata to the database.
    /// </summary>
    public class RunManager : BackgroundService
    {
        private readonly Channel<RunRequest> _queue = Channel.CreateUnbounded<RunRequest>();
        private readonly IServiceProvider _services;
        private readonly ILogger<RunManager> _logger;
        private readonly IHubContext<RunsHub> _hub;

        public RunManager(IServiceProvider services, ILogger<RunManager> logger, IHubContext<RunsHub> hub)
        {
            _services = services;
            _logger = logger;
            _hub = hub;
        }

        /// <summary>
        /// Enqueue a run request and return the persisted WebRun id.
        /// </summary>
        public async Task<Guid> EnqueueAsync(RunRequest request)
        {
            // Persist a WebRun entry immediately
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AcadSyncWebDbContext>();

            var webRun = new WebRun
            {
                Mode = request.Mode,
                Status = "Queued",
                Notes = request.Notes,
                StartedAt = DateTime.UtcNow
            };

            db.WebRuns.Add(webRun);
            await db.SaveChangesAsync();

            request.WebRunId = webRun.Id;
            await _queue.Writer.WriteAsync(request);
            _logger.LogInformation("Enqueued run {RunId} mode={Mode}", webRun.Id, request.Mode);

            // notify clients
            await _hub.Clients.All.SendAsync("runQueued", new { runId = webRun.Id, mode = webRun.Mode });

            return webRun.Id;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RunManager started");
            var reader = _queue.Reader;

            while (await reader.WaitToReadAsync(stoppingToken))
            {
                while (reader.TryRead(out var request))
                {
                    try
                    {
                        await ProcessRunAsync(request, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Run processing failed for {RunId}", request.WebRunId);
                        await MarkRunFailedAsync(request.WebRunId, ex.Message);
                    }
                }
            }
        }

        private async Task ProcessRunAsync(RunRequest request, CancellationToken cancellationToken)
        {
            if (request.WebRunId == null)
                throw new InvalidOperationException("Request must have persisted WebRunId before processing.");

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AcadSyncWebDbContext>();
            var validationService = scope.ServiceProvider.GetRequiredService<ExtPropValidationService>();

            var webRun = await db.WebRuns.FindAsync(request.WebRunId.Value);
            if (webRun == null)
                throw new InvalidOperationException("WebRun record not found");

            webRun.Status = "Running";
            webRun.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // notify clients
            await _hub.Clients.All.SendAsync("runStarted", new { runId = webRun.Id, mode = webRun.Mode });

            try
            {
                switch (request.Mode.ToLowerInvariant())
                {
                    case "demo":
var demoViolations = await validationService.ValidateAllAsync(EprlMode.validate);
webRun.ViolationsFound = demoViolations.Count();
                        break;

                    case "validate":
var v = await validationService.ValidateAllAsync(EprlMode.validate);
webRun.ViolationsFound = v.Count();
                        break;

                    case "simulate":
var s = await validationService.ValidateAllAsync(EprlMode.simulate);
webRun.ViolationsFound = s.Count();
                        break;

                    case "repair":
var repaired = await validationService.ValidateAndRepairAsync(request.StaffId ?? 1);
webRun.ViolationsProcessed = repaired.Count();
webRun.ViolationsFound = repaired.Count();
                        break;

                    case "revert":
                        // support optional runId and fromDate; prefer runId if provided
                        if (request.TargetRunId.HasValue)
                        {
                            var revByRun = await validationService.RevertByRunIdAsync(request.TargetRunId.Value, request.Force, request.StaffId ?? 1, request.DryRun);
                            webRun.ViolationsProcessed = revByRun.SuccessfulRepairs + revByRun.FailedRepairs;
                        }
                        else if (request.FromDate.HasValue)
                        {
                            var revByDate = await validationService.RevertByDateRangeAsync(request.FromDate.Value, null, request.Force, request.StaffId ?? 1, request.DryRun);
                            webRun.ViolationsProcessed = revByDate.SuccessfulRepairs + revByDate.FailedRepairs;
                        }
                        else
                        {
                            var fallback = DateTimeOffset.UtcNow.AddHours(-1);
                            var rev = await validationService.RevertRepairsAsync(fallback, request.Force, request.StaffId ?? 1, request.DryRun);
                            webRun.ViolationsProcessed = rev.SuccessfulRepairs + rev.FailedRepairs;
                        }
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown run mode: {request.Mode}");
                }

                webRun.Status = "Completed";
                webRun.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                // notify clients
                await _hub.Clients.All.SendAsync("runCompleted", new { runId = webRun.Id, status = webRun.Status, violationsFound = webRun.ViolationsFound, violationsProcessed = webRun.ViolationsProcessed });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Run {RunId} failed", webRun.Id);
                webRun.Status = "Failed";
                webRun.Notes = ex.Message;
                webRun.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                await _hub.Clients.All.SendAsync("runFailed", new { runId = webRun.Id, error = ex.Message });
            }
        }

        private async Task MarkRunFailedAsync(Guid? webRunId, string message)
        {
            if (webRunId == null) return;

            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AcadSyncWebDbContext>();
                var webRun = await db.WebRuns.FindAsync(webRunId.Value);
                if (webRun != null)
                {
                    webRun.Status = "Failed";
                    webRun.Notes = message;
                    webRun.FinishedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();

                    await _hub.Clients.All.SendAsync("runFailed", new { runId = webRun.Id, error = message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark run failed for {RunId}", webRunId);
            }
        }
    }

    public class RunRequest
    {
        public Guid? WebRunId { get; set; }
        public string Mode { get; set; } = null!;
        public long? TargetRunId { get; set; } // for revert by runId
        public DateTimeOffset? FromDate { get; set; } // revert from date
        public bool Force { get; set; } = false;
        public bool DryRun { get; set; } = false;
        public int? StaffId { get; set; }
        public string? Notes { get; set; }
    }
}
