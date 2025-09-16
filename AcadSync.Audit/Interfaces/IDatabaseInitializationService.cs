namespace AcadSync.Audit.Interfaces;

/// <summary>
/// Service interface for audit database initialization
/// </summary>
public interface IDatabaseInitializationService
{
    /// <summary>
    /// Initialize the audit database with all required objects
    /// </summary>
    Task InitializeAsync();
}
