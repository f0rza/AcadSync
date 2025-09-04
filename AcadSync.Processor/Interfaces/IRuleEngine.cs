using AcadSync.Processor.Models.Domain;
using AcadSync.Processor.Models.Projections;

namespace AcadSync.Processor.Interfaces;

/// <summary>
/// Core rule evaluation engine interface
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Evaluate rules against a collection of entities
    /// </summary>
    /// <param name="doc">The rule document to evaluate</param>
    /// <param name="entities">Entities to validate</param>
    /// <returns>Collection of violations found</returns>
    Task<IEnumerable<Violation>> EvaluateAsync(EprlDoc doc, IEnumerable<IEntityProjection> entities);

    /// <summary>
    /// Evaluate rules against a single entity
    /// </summary>
    /// <param name="doc">The rule document to evaluate</param>
    /// <param name="entity">Entity to validate</param>
    /// <returns>Collection of violations found for the entity</returns>
    Task<IEnumerable<Violation>> EvaluateEntityAsync(EprlDoc doc, IEntityProjection entity);
}
