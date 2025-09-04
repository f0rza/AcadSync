using Microsoft.Extensions.Logging;
using AcadSync.Processor.Interfaces;

namespace AcadSync.Processor.Services;

/// <summary>
/// Entity data retrieval orchestration service
/// </summary>
public class EntityService : IEntityService
{
    private readonly IExtPropRepository _repository;
    private readonly ILogger<EntityService> _logger;

    public EntityService(IExtPropRepository repository, ILogger<EntityService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<StudentProjection>> GetStudentsAsync(string? programFilter = null, string? statusFilter = null)
    {
        _logger.LogDebug("Retrieving students with filters - Program: {ProgramFilter}, Status: {StatusFilter}", 
            programFilter ?? "None", statusFilter ?? "None");

        try
        {
            var students = await _repository.GetStudentsAsync(programFilter, statusFilter);
            
            _logger.LogInformation("Retrieved {StudentCount} students", students.Count);
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var programGroups = students.GroupBy(s => s.programCode).ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
                var statusGroups = students.GroupBy(s => s.status).ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
                
                _logger.LogDebug("Students by program: {ProgramBreakdown}", string.Join(", ", programGroups.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
                _logger.LogDebug("Students by status: {StatusBreakdown}", string.Join(", ", statusGroups.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
            }

            return students;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve students with filters - Program: {ProgramFilter}, Status: {StatusFilter}", 
                programFilter, statusFilter);
            throw;
        }
    }

    public async Task<IEnumerable<DocumentProjection>> GetDocumentsAsync(string? docTypeFilter = null)
    {
        _logger.LogDebug("Retrieving documents with filter - DocType: {DocTypeFilter}", docTypeFilter ?? "None");

        try
        {
            var documents = await _repository.GetDocumentsAsync(docTypeFilter);
            
            _logger.LogInformation("Retrieved {DocumentCount} documents", documents.Count);
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var typeGroups = documents.GroupBy(d => d.documentTypeCode).ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
                _logger.LogDebug("Documents by type: {TypeBreakdown}", string.Join(", ", typeGroups.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve documents with filter - DocType: {DocTypeFilter}", docTypeFilter);
            throw;
        }
    }

    public async Task<IEnumerable<IEntityProjection>> GetAllEntitiesAsync(
        string? studentProgramFilter = null, 
        string? studentStatusFilter = null, 
        string? docTypeFilter = null)
    {
        _logger.LogDebug("Retrieving all entities with filters - StudentProgram: {StudentProgramFilter}, StudentStatus: {StudentStatusFilter}, DocType: {DocTypeFilter}", 
            studentProgramFilter ?? "None", studentStatusFilter ?? "None", docTypeFilter ?? "None");

        try
        {
            // Retrieve students and documents in parallel
            var studentsTask = GetStudentsAsync(studentProgramFilter, studentStatusFilter);
            var documentsTask = GetDocumentsAsync(docTypeFilter);

            await Task.WhenAll(studentsTask, documentsTask);

            var students = await studentsTask;
            var documents = await documentsTask;

            // Combine all entities
            var allEntities = new List<IEntityProjection>();
            allEntities.AddRange(students);
            allEntities.AddRange(documents);

            _logger.LogInformation("Retrieved {TotalEntityCount} total entities ({StudentCount} students, {DocumentCount} documents)", 
                allEntities.Count, students.Count(), documents.Count());

            return allEntities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve all entities");
            throw;
        }
    }

    public async Task<StudentProjection?> GetStudentByIdAsync(long studentId)
    {
        _logger.LogDebug("Retrieving student by ID: {StudentId}", studentId);

        try
        {
            // Since the repository doesn't have a GetByID method, we'll filter after retrieval
            // In a production system, you'd want to add a specific method to the repository
            var allStudents = await _repository.GetStudentsAsync();
            var student = allStudents.FirstOrDefault(s => s.id == studentId);

            if (student != null)
            {
                _logger.LogDebug("Found student: {StudentNumber} (ID: {StudentId})", student.studentNumber, studentId);
            }
            else
            {
                _logger.LogDebug("Student not found with ID: {StudentId}", studentId);
            }

            return student;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve student by ID: {StudentId}", studentId);
            throw;
        }
    }

    public async Task<DocumentProjection?> GetDocumentByIdAsync(long documentId)
    {
        _logger.LogDebug("Retrieving document by ID: {DocumentId}", documentId);

        try
        {
            // Since the repository doesn't have a GetByID method, we'll filter after retrieval
            // In a production system, you'd want to add a specific method to the repository
            var allDocuments = await _repository.GetDocumentsAsync();
            var document = allDocuments.FirstOrDefault(d => d.id == documentId);

            if (document != null)
            {
                _logger.LogDebug("Found document: {DocumentType} (ID: {DocumentId})", document.documentTypeCode, documentId);
            }
            else
            {
                _logger.LogDebug("Document not found with ID: {DocumentId}", documentId);
            }

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve document by ID: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        _logger.LogDebug("Testing database connection");

        try
        {
            var isConnected = await _repository.TestConnectionAsync();
            
            if (isConnected)
            {
                _logger.LogInformation("Database connection test successful");
            }
            else
            {
                _logger.LogWarning("Database connection test failed");
            }

            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection test threw an exception");
            return false;
        }
    }
}
