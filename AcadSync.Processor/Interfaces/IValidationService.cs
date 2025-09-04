using AcadSync.Processor.Models.Results;

namespace AcadSync.Processor.Interfaces;

/// <summary>
/// Main validation orchestration service interface
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validate all entities against rules without making changes
    /// </summary>
    /// <param name="mode">Validation mode (validate, simulate, repair)</param>
    /// <returns>Validation result with violations found</returns>
    Task<ValidationResult> ValidateAllAsync(EprlMode mode = EprlMode.validate);

    /// <summary>
    /// Validate specific entities against rules
    /// </summary>
    /// <param name="entities">Entities to validate</param>
    /// <param name="mode">Validation mode (validate, simulate, repair)</param>
    /// <returns>Validation result with violations found</returns>
    Task<ValidationResult> ValidateEntitiesAsync(IEnumerable<IEntityProjection> entities, EprlMode mode = EprlMode.validate);

    /// <summary>
    /// Validate using custom rules from file path
    /// </summary>
    /// <param name="rulesFilePath">Path to rules file</param>
    /// <param name="mode">Validation mode (validate, simulate, repair)</param>
    /// <returns>Validation result with violations found</returns>
    Task<ValidationResult> ValidateWithCustomRulesAsync(string rulesFilePath, EprlMode mode = EprlMode.validate);

    /// <summary>
    /// Get extended property definitions for diagnostics
    /// </summary>
    /// <param name="entityType">Entity type to get definitions for</param>
    /// <returns>Collection of property definitions</returns>
    Task<IEnumerable<ExtPropertyDefinition>> GetPropertyDefinitionsAsync(string entityType);

    /// <summary>
    /// Test system connectivity and configuration
    /// </summary>
    /// <returns>System health check result</returns>
    Task<SystemHealthResult> TestSystemHealthAsync();
}
