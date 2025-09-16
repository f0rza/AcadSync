using AcadSync.Processor.Models.Results;

namespace AcadSync.Processor.Interfaces;

/// <summary>
/// Interface for violation repair operations
/// </summary>
public interface IRepairService
{
    /// <summary>
    /// Apply repairs for all violations that can be automatically fixed
    /// </summary>
    /// <param name="violations">Violations to repair</param>
    /// <param name="staffId">Staff ID performing the repair</param>
    /// <returns>Repair result with success/failure details</returns>
    Task<RepairResult> RepairViolationsAsync(IEnumerable<Violation> violations, int staffId = 1);

    /// <summary>
    /// Apply repair for a single violation
    /// </summary>
    /// <param name="violation">Violation to repair</param>
    /// <param name="staffId">Staff ID performing the repair</param>
    /// <returns>True if repair was successful</returns>
    Task<bool> RepairViolationAsync(Violation violation, int staffId = 1);

    /// <summary>
    /// Check if a violation can be automatically repaired
    /// </summary>
    /// <param name="violation">Violation to check</param>
    /// <returns>True if the violation can be repaired</returns>
    bool CanRepairViolation(Violation violation);

    /// <summary>
    /// Get all repairable violations from a collection
    /// </summary>
    /// <param name="violations">All violations</param>
    /// <returns>Only the violations that can be repaired</returns>
    IEnumerable<Violation> GetRepairableViolations(IEnumerable<Violation> violations);

    /// <summary>
    /// Validate and repair in a single operation
    /// </summary>
    /// <param name="entities">Entities to validate and repair</param>
    /// <param name="staffId">Staff ID performing the repair</param>
    /// <returns>Combined validation and repair result</returns>
    Task<ValidationAndRepairResult> ValidateAndRepairAsync(IEnumerable<IEntityProjection> entities, int staffId = 1);
}
