using Dapper;
using Microsoft.Data.SqlClient;

namespace AcadSync.Processor;

/// <summary>
/// Dapper-based repository for AcadSync audit database operations
/// </summary>
public class AuditRepository : IAuditRepository
{
    private readonly string _connectionString;

    public AuditRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task WriteAuditAsync(Violation violation, int staffId, string? notes = null)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            INSERT INTO acadsync.ExtPropAudit (
                Timestamp, RuleId, EntityType, EntityId, PropertyCode,
                OldValue, NewValue, Action, Severity, Operator, Notes
            ) VALUES (
                SYSDATETIMEOFFSET(), @RuleId, @EntityType, @EntityId, @PropertyCode,
                @OldValue, @NewValue, @Action, @Severity, @Operator, @Notes
            )";

        await connection.ExecuteAsync(sql, new
        {
            RuleId = violation.RuleId,
            EntityType = violation.EntityType,
            EntityId = violation.EntityId,
            PropertyCode = violation.PropertyCode,
            OldValue = violation.CurrentValue,
            NewValue = violation.ProposedValue,
            Action = violation.Action,
            Severity = violation.Severity.ToString(),
            Operator = $"staff:{staffId}",
            Notes = notes
        });
    }

    public async Task<long> StartValidationRunAsync(string mode, int staffId, string? notes = null)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            INSERT INTO acadsync.ValidationRuns (
                StartTime, Mode, StaffId, Status, Notes
            ) 
            OUTPUT INSERTED.RunId
            VALUES (
                SYSDATETIMEOFFSET(), @Mode, @StaffId, 'Running', @Notes
            )";

        var runId = await connection.QuerySingleAsync<long>(sql, new
        {
            Mode = mode,
            StaffId = staffId,
            Notes = notes
        });

        return runId;
    }

    public async Task CompleteValidationRunAsync(long runId, int violationCount, int repairedCount, string? notes = null)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            UPDATE acadsync.ValidationRuns 
            SET 
                EndTime = SYSDATETIMEOFFSET(),
                Status = 'Completed',
                ViolationCount = @ViolationCount,
                RepairedCount = @RepairedCount,
                Notes = COALESCE(@Notes, Notes)
            WHERE RunId = @RunId";

        await connection.ExecuteAsync(sql, new
        {
            RunId = runId,
            ViolationCount = violationCount,
            RepairedCount = repairedCount,
            Notes = notes
        });
    }

    public async Task LogSystemEventAsync(string level, string message, string? exception = null, string? source = null)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            INSERT INTO acadsync.SystemLog (
                Timestamp, Level, Message, Exception, Source
            ) VALUES (
                SYSDATETIMEOFFSET(), @Level, @Message, @Exception, @Source
            )";

        await connection.ExecuteAsync(sql, new
        {
            Level = level,
            Message = message,
            Exception = exception,
            Source = source ?? "AcadSync"
        });
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

    public async Task<AuditStatistics> GetAuditStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var whereClause = "";
        var parameters = new DynamicParameters();
        
        if (fromDate.HasValue)
        {
            whereClause += " AND a.Timestamp >= @FromDate";
            parameters.Add("FromDate", fromDate.Value);
        }
        
        if (toDate.HasValue)
        {
            whereClause += " AND a.Timestamp <= @ToDate";
            parameters.Add("ToDate", toDate.Value);
        }

        var sql = $@"
            -- Total violations and repairs
            SELECT 
                COUNT(*) as TotalViolations,
                COUNT(CASE WHEN Action LIKE 'repair:%' THEN 1 END) as TotalRepairs
            FROM acadsync.ExtPropAudit a
            WHERE 1=1 {whereClause};

            -- Violations by rule
            SELECT RuleId, COUNT(*) as Count
            FROM acadsync.ExtPropAudit a
            WHERE 1=1 {whereClause}
            GROUP BY RuleId;

            -- Violations by severity
            SELECT Severity, COUNT(*) as Count
            FROM acadsync.ExtPropAudit a
            WHERE 1=1 {whereClause}
            GROUP BY Severity;

            -- Validation runs count and last run
            SELECT 
                COUNT(*) as ValidationRuns,
                MAX(StartTime) as LastRunDate
            FROM acadsync.ValidationRuns r
            WHERE 1=1 {whereClause.Replace("a.Timestamp", "r.StartTime")};";

        using var multi = await connection.QueryMultipleAsync(sql, parameters);
        
        var totals = await multi.ReadSingleAsync<(int TotalViolations, int TotalRepairs)>();
        var ruleStats = (await multi.ReadAsync<(string RuleId, int Count)>())
            .ToDictionary(x => x.RuleId, x => x.Count);
        var severityStats = (await multi.ReadAsync<(string Severity, int Count)>())
            .ToDictionary(x => Enum.Parse<Severity>(x.Severity), x => x.Count);
        var runStats = await multi.ReadSingleAsync<(int ValidationRuns, DateTime? LastRunDate)>();

        return new AuditStatistics(
            totals.TotalViolations,
            totals.TotalRepairs,
            runStats.ValidationRuns,
            ruleStats,
            severityStats,
            runStats.LastRunDate
        );
    }

    public async Task CleanupOldAuditRecordsAsync(int retentionDays)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        
        var sql = @"
            DELETE FROM acadsync.ExtPropAudit 
            WHERE Timestamp < @CutoffDate;
            
            DELETE FROM acadsync.ValidationRuns 
            WHERE StartTime < @CutoffDate;
            
            DELETE FROM acadsync.SystemLog 
            WHERE Timestamp < @CutoffDate;";

        await connection.ExecuteAsync(sql, new { CutoffDate = cutoffDate });
    }
}
