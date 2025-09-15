using AcadSync.Processor.Models.Results;
using AcadSync.Audit.Models;

namespace AcadSync.Processor.Interfaces;

/// <summary>
/// Service interface for reverting repair operations
/// </summary>
public interface IRevertService
{
    /// <summary>
    /// Revert a collection of repair operations
    /// </summary>
    Task<RepairResult> RevertAsync(IEnumerable<AuditEntry> repairs, bool force = false, int staffId = 1, bool dryRun = false, long? runId = null);

    /// <summary>
    /// Revert repairs based on filter criteria
    /// </summary>
    Task<RepairResult> RevertByFilterAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? ruleId = null,
        string? entityType = null,
        long? entityId = null,
        long? runId = null,
        bool force = false,
        int staffId = 1,
        bool dryRun = false);
}
