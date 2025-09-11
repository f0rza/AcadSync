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
                s.StuNum as StudentNumber,
                CAST(s.AdProgramID AS NVARCHAR(50)) as ProgramCode,
                'Active' as Status, -- Default status since no Status column exists
                'Main' as Campus, -- Default campus since no Campus column exists
                'Unknown' as Citizenship, -- Default since no Citizenship column exists
                'Unknown' as VisaType, -- Default since no VisaType column exists
                'Unknown' as Country, -- Default since no Country column exists
                -- Extended Properties
                ep.Name as PropertyCode,
                COALESCE(epv.StringValue, CAST(epv.DecimalValue AS NVARCHAR(512)), CAST(epv.DateTimeValue AS NVARCHAR(512)), CAST(epv.BooleanValue AS NVARCHAR(512)), epv.MultiValue) as ExtValue
            FROM dbo.syStudent s WITH (NOLOCK)
            LEFT JOIN dbo.SyExtendedPropertyValue epv WITH (NOLOCK) ON epv.EntityId = s.SyStudentId
            LEFT JOIN dbo.SyExtendedPropertyDefinition ep WITH (NOLOCK) ON ep.SyExtendedPropertyDefinitionId = epv.SyExtendedPropertyDefinitionId AND ep.EntityName = 'syStudent'
            WHERE s.Active = 1
                AND ep.IsActive = 1
                AND (@ProgramFilter IS NULL OR CAST(s.AdProgramID AS NVARCHAR(50)) = @ProgramFilter)
                AND (@StatusFilter IS NULL OR 'Active' = @StatusFilter)
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
                CAST(d.CmDocTypeID AS NVARCHAR(50)) as DocumentTypeCode,
                -- Extended Properties
                ep.Name as PropertyCode,
                COALESCE(epv.StringValue, CAST(epv.DecimalValue AS NVARCHAR(512)), CAST(epv.DateTimeValue AS NVARCHAR(512)), CAST(epv.BooleanValue AS NVARCHAR(512)), epv.MultiValue) as ExtValue
            FROM dbo.CmDocument d WITH (NOLOCK)
            LEFT JOIN dbo.SyExtendedPropertyValue epv WITH (NOLOCK) ON epv.EntityId = d.CmDocumentId
            LEFT JOIN dbo.SyExtendedPropertyDefinition ep WITH (NOLOCK) ON ep.SyExtendedPropertyDefinitionId = epv.SyExtendedPropertyDefinitionId AND ep.EntityName = 'Document'
            WHERE 1=1 -- No IsActive column in CmDocument, so always true
                AND ep.IsActive = 1
                AND (@DocTypeFilter IS NULL OR CAST(d.CmDocTypeID AS NVARCHAR(50)) = @DocTypeFilter)
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
        
        // Use the property name version for flexibility
        var sql = @"
            DECLARE @Now DATETIME = GETDATE();

            ;WITH Def AS (
                SELECT sp.SyExtendedPropertyDefinitionId
                FROM dbo.SyExtendedPropertyDefinition sp
                WHERE sp.EntityName = @EntityName AND sp.Name = @PropertyName
            )
            MERGE dbo.SyExtendedPropertyValue AS T
            USING (SELECT @EntityId AS EntityId, d.SyExtendedPropertyDefinitionId AS PropertyId FROM Def d) AS S
            ON (T.EntityId = S.EntityId AND T.SyExtendedPropertyDefinitionId = S.PropertyId)
            WHEN MATCHED AND ISNULL(T.StringValue, N'') <> ISNULL(@NewValue, N'')
              THEN UPDATE SET
                   T.StringValue = @NewValue,
                   T.DateLstMod = @Now,
                   T.UserId = @StaffId
            WHEN NOT MATCHED BY TARGET
              THEN INSERT (EntityName, EntityId, SyExtendedPropertyDefinitionId, StringValue, DateAdded, UserId)
                   VALUES (@EntityName, @EntityId, S.PropertyId, @NewValue, @Now, @StaffId);";

        await connection.ExecuteAsync(sql, new
        {
            EntityName = entityType,
            EntityId = entityId,
            PropertyName = propertyCode,
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
            SELECT SyExtendedPropertyDefinitionId
            FROM dbo.SyExtendedPropertyDefinition WITH (NOLOCK)
            WHERE EntityName = @EntityType AND Name = @PropertyCode AND IsActive = 1";

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
                SyExtendedPropertyDefinitionId as PropertyId,
                Name as PropertyCode,
                EntityName as EntityTable,
                Name as DisplayName,
                PropertyType as DataType,
                CAST(0 AS BIT) as IsRequired, -- Default since no IsRequired column
                IsActive
            FROM dbo.SyExtendedPropertyDefinition WITH (NOLOCK)
            WHERE EntityName = @EntityType
            ORDER BY Name";

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
