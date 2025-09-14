using Microsoft.Data.SqlClient;

namespace AcadSync.Processor;

/// <summary>
/// Repository interface for Extended Property operations against Anthology Student database
/// </summary>
public interface IExtPropRepository
{
    /// <summary>
    /// Get students with their extended properties for validation
    /// </summary>
    Task<List<StudentProjection>> GetStudentsAsync(string? programFilter = null, string? statusFilter = null);

    /// <summary>
    /// Get documents with their extended properties for validation
    /// </summary>
    Task<List<DocumentProjection>> GetDocumentsAsync(string? docTypeFilter = null);

    /// <summary>
    /// Upsert an extended property value for an entity
    /// </summary>
    Task UpsertExtPropertyAsync(string entityType, long entityId, string propertyCode, string? newValue, int staffId);

    /// <summary>
    /// Resolve property code to internal PropertyId (cached for performance)
    /// </summary>
    Task<int?> ResolvePropertyIdAsync(string entityType, string propertyCode);

    /// <summary>
    /// Get extended property definitions for an entity type
    /// </summary>
    Task<List<ExtPropertyDefinition>> GetPropertyDefinitionsAsync(string entityType);

    /// <summary>
    /// Get current property value directly from SyExtendedPropertyValue table
    /// </summary>
    Task<string?> GetCurrentPropertyValueAsync(string entityType, long entityId, string propertyCode);

    /// <summary>
    /// Test database connection
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Delete the extended property value for an entity (returns true if a row was deleted)
    /// </summary>
    Task<bool> DeleteExtPropertyAsync(string entityType, long entityId, string propertyCode);
}

/// <summary>
/// Extended Property Definition from Anthology Student
/// </summary>
public sealed record ExtPropertyDefinition(
    int PropertyId,
    string PropertyCode,
    string EntityTable,
    string? DisplayName,
    string? DataType,
    bool IsRequired,
    bool IsActive
);
