using Dapper;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;

namespace AcadSync.Processor;

/// <summary>
/// Dapper-based repository for Anthology Student Extended Properties
/// </summary>
public class AnthologyExtPropRepository : IExtPropRepository
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<string, int> _propertyIdCache = new();

    public AnthologyExtPropRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<List<StudentProjection>> GetStudentsAsync(string? programFilter = null, string? statusFilter = null)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            SELECT 
                s.SyStudentId as Id,
                s.StudentNumber,
                s.ProgramCode,
                s.Status,
                s.Campus,
                s.Citizenship,
                s.VisaType,
                s.Country,
                -- Extended Properties
                ep.PropertyCode,
                epv.Value as ExtValue
            FROM dbo.SyStudent s WITH (NOLOCK)
            LEFT JOIN dbo.SyExtendedPropertyValue epv WITH (NOLOCK) ON epv.EntityTable = 'SyStudent' AND epv.EntityId = s.SyStudentId
            LEFT JOIN dbo.SyExtendedProperty ep WITH (NOLOCK) ON ep.SyExtendedPropertyId = epv.SyExtendedPropertyId
            WHERE s.IsActive = 1
                AND (@ProgramFilter IS NULL OR s.ProgramCode = @ProgramFilter)
                AND (@StatusFilter IS NULL OR s.Status = @StatusFilter)
            ORDER BY s.SyStudentId";

        var studentDict = new Dictionary<long, StudentProjection>();
        
        await connection.QueryAsync<dynamic>(sql, new { ProgramFilter = programFilter, StatusFilter = statusFilter })
            .ContinueWith(task =>
            {
                foreach (var row in task.Result)
                {
                    long id = row.Id;
                    
                    if (!studentDict.TryGetValue(id, out var student))
                    {
                        student = new StudentProjection(
                            id: id,
                            studentNumber: row.StudentNumber,
                            programCode: row.ProgramCode,
                            status: row.Status,
                            campus: row.Campus,
                            citizenship: row.Citizenship,
                            visaType: row.VisaType,
                            country: row.Country,
                            documents: new List<DocumentItem>(), // TODO: Load documents if needed
                            ext: new Dictionary<string, string?>()
                        );
                        studentDict[id] = student;
                    }

                    // Add extended property if present
                    if (row.PropertyCode != null)
                    {
                        student.Ext[row.PropertyCode] = row.ExtValue;
                    }
                }
            });

        return studentDict.Values.ToList();
    }

    public async Task<List<DocumentProjection>> GetDocumentsAsync(string? docTypeFilter = null)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            SELECT 
                d.CmDocumentId as Id,
                d.DocumentTypeCode,
                -- Extended Properties
                ep.PropertyCode,
                epv.Value as ExtValue
            FROM dbo.CmDocument d WITH (NOLOCK)
            LEFT JOIN dbo.SyExtendedPropertyValue epv WITH (NOLOCK) ON epv.EntityTable = 'CmDocument' AND epv.EntityId = d.CmDocumentId
            LEFT JOIN dbo.SyExtendedProperty ep WITH (NOLOCK) ON ep.SyExtendedPropertyId = epv.SyExtendedPropertyId
            WHERE d.IsActive = 1
                AND (@DocTypeFilter IS NULL OR d.DocumentTypeCode = @DocTypeFilter)
            ORDER BY d.CmDocumentId";

        var docDict = new Dictionary<long, DocumentProjection>();
        
        await connection.QueryAsync<dynamic>(sql, new { DocTypeFilter = docTypeFilter })
            .ContinueWith(task =>
            {
                foreach (var row in task.Result)
                {
                    long id = row.Id;
                    
                    if (!docDict.TryGetValue(id, out var doc))
                    {
                        doc = new DocumentProjection(
                            id: id,
                            documentTypeCode: row.DocumentTypeCode,
                            meta: new Dictionary<string, object?>(),
                            ext: new Dictionary<string, string?>()
                        );
                        docDict[id] = doc;
                    }

                    // Add extended property if present
                    if (row.PropertyCode != null)
                    {
                        doc.Ext[row.PropertyCode] = row.ExtValue;
                    }
                }
            });

        return docDict.Values.ToList();
    }

    public async Task UpsertExtPropertyAsync(string entityType, long entityId, string propertyCode, string? newValue, int staffId)
    {
        using var connection = new SqlConnection(_connectionString);
        
        // Use the property code version for flexibility
        var sql = @"
            DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();
            
            ;WITH Def AS (
                SELECT sp.SyExtendedPropertyId
                FROM dbo.SyExtendedProperty sp
                WHERE sp.EntityTable = @EntityTable AND sp.PropertyCode = @PropertyCode
            )
            MERGE dbo.SyExtendedPropertyValue AS T
            USING (SELECT @EntityId AS EntityId, d.SyExtendedPropertyId AS PropertyId FROM Def d) AS S
            ON (T.EntityTable = @EntityTable AND T.EntityId = S.EntityId AND T.SyExtendedPropertyId = S.PropertyId)
            WHEN MATCHED AND ISNULL(T.[Value], N'') <> ISNULL(@NewValue, N'')
              THEN UPDATE SET
                   T.[Value] = @NewValue,
                   T.RecLastModifiedDate = @Now,
                   T.UpdatedByStaffId = @StaffId
            WHEN NOT MATCHED BY TARGET
              THEN INSERT (EntityTable, EntityId, SyExtendedPropertyId, [Value], RecCreateDate, CreatedByStaffId)
                   VALUES (@EntityTable, @EntityId, S.PropertyId, @NewValue, @Now, @StaffId);";

        await connection.ExecuteAsync(sql, new
        {
            EntityTable = entityType,
            EntityId = entityId,
            PropertyCode = propertyCode,
            NewValue = newValue,
            StaffId = staffId
        });
    }

    public async Task<int?> ResolvePropertyIdAsync(string entityType, string propertyCode)
    {
        var cacheKey = $"{entityType}:{propertyCode}";
        
        if (_propertyIdCache.TryGetValue(cacheKey, out var cachedId))
            return cachedId;

        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            SELECT SyExtendedPropertyId 
            FROM dbo.SyExtendedProperty WITH (NOLOCK)
            WHERE EntityTable = @EntityType AND PropertyCode = @PropertyCode AND IsActive = 1";

        var propertyId = await connection.QuerySingleOrDefaultAsync<int?>(sql, new
        {
            EntityType = entityType,
            PropertyCode = propertyCode
        });

        if (propertyId.HasValue)
        {
            _propertyIdCache.TryAdd(cacheKey, propertyId.Value);
        }

        return propertyId;
    }

    public async Task<List<ExtPropertyDefinition>> GetPropertyDefinitionsAsync(string entityType)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            SELECT 
                SyExtendedPropertyId as PropertyId,
                PropertyCode,
                EntityTable,
                DisplayName,
                DataType,
                IsRequired,
                IsActive
            FROM dbo.SyExtendedProperty WITH (NOLOCK)
            WHERE EntityTable = @EntityType
            ORDER BY PropertyCode";

        var results = await connection.QueryAsync<ExtPropertyDefinition>(sql, new { EntityType = entityType });
        return results.ToList();
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            var result = await connection.QuerySingleAsync<int>("SELECT 1");
            return result == 1;
        }
        catch
        {
            return false;
        }
    }
}
