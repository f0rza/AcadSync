namespace AcadSync.Processor.Interfaces;

/// <summary>
/// Interface for entity data retrieval and management
/// </summary>
public interface IEntityService
{
    /// <summary>
    /// Get all students with optional filtering
    /// </summary>
    /// <param name="programFilter">Optional program code filter</param>
    /// <param name="statusFilter">Optional status filter</param>
    /// <returns>Collection of student projections</returns>
    Task<IEnumerable<StudentProjection>> GetStudentsAsync(string? programFilter = null, string? statusFilter = null);

    /// <summary>
    /// Get all documents with optional filtering
    /// </summary>
    /// <param name="docTypeFilter">Optional document type filter</param>
    /// <returns>Collection of document projections</returns>
    Task<IEnumerable<DocumentProjection>> GetDocumentsAsync(string? docTypeFilter = null);

    /// <summary>
    /// Get all entities (students and documents) combined
    /// </summary>
    /// <param name="studentProgramFilter">Optional student program filter</param>
    /// <param name="studentStatusFilter">Optional student status filter</param>
    /// <param name="docTypeFilter">Optional document type filter</param>
    /// <returns>Collection of all entity projections</returns>
    Task<IEnumerable<IEntityProjection>> GetAllEntitiesAsync(
        string? studentProgramFilter = null, 
        string? studentStatusFilter = null, 
        string? docTypeFilter = null);

    /// <summary>
    /// Get a specific student by ID
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <returns>Student projection or null if not found</returns>
    Task<StudentProjection?> GetStudentByIdAsync(long studentId);

    /// <summary>
    /// Get a specific document by ID
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <returns>Document projection or null if not found</returns>
    Task<DocumentProjection?> GetDocumentByIdAsync(long documentId);

    /// <summary>
    /// Test connectivity to the data source
    /// </summary>
    /// <returns>True if connection is successful</returns>
    Task<bool> TestConnectionAsync();
}
