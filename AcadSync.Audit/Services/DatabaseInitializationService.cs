using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using AcadSync.Audit.Interfaces;

namespace AcadSync.Audit.Services;

public class DatabaseInitializationService : IDatabaseInitializationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(
        IConfiguration configuration,
        ILogger<DatabaseInitializationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var auditConnectionString = _configuration.GetConnectionString("AcadSyncAudit");
            if (string.IsNullOrEmpty(auditConnectionString))
            {
                _logger.LogWarning("AcadSyncAudit connection string not found. Skipping database initialization.");
                return;
            }

            _logger.LogInformation("Starting database initialization...");

            // Check if auto-initialization is enabled (default: true)
            var autoInitEnabled = _configuration.GetValue("AcadSync:AutoInitializeDatabase", true);
            if (!autoInitEnabled)
            {
                _logger.LogInformation("Database auto-initialization is disabled in configuration.");
                return;
            }

            // Load the SQL script from embedded resource
            var sqlScript = await LoadSqlScriptAsync();
            if (string.IsNullOrEmpty(sqlScript))
            {
                _logger.LogError("Failed to load database initialization script.");
                return;
            }

            // Replace database name placeholder in script with actual database name from connection string
            var databaseName = GetDatabaseNameFromConnectionString(auditConnectionString);
            var processedScript = sqlScript.Replace("AcadSyncAudit", databaseName);

            // Execute the script
            await ExecuteSqlScriptAsync(auditConnectionString, processedScript);

            _logger.LogInformation("Database initialization completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database: {Message}", ex.Message);
            throw;
        }
    }

    private async Task<string> LoadSqlScriptAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "AcadSync.Audit.SqlScripts.CreateAuditDatabase.sql";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                // Fallback to file system if embedded resource not found
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SqlScripts", "CreateAuditDatabase.sql");
                if (File.Exists(scriptPath))
                {
                    _logger.LogInformation("Loading SQL script from file system: {Path}", scriptPath);
                    return await File.ReadAllTextAsync(scriptPath);
                }

                _logger.LogError("SQL script not found as embedded resource or file: {ResourceName}", resourceName);
                return string.Empty;
            }

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            _logger.LogDebug("Loaded SQL script from embedded resource: {ResourceName}", resourceName);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SQL script: {Message}", ex.Message);
            return string.Empty;
        }
    }

    private async Task ExecuteSqlScriptAsync(string connectionString, string sqlScript)
    {
        // Split script by GO statements and execute each batch separately
        var batches = SplitSqlScript(sqlScript);
        
        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            if (string.IsNullOrWhiteSpace(batch))
                continue;

            try
            {
                // For database creation operations, connect to master database
                var effectiveConnectionString = connectionString;
                if (batch.Contains("CREATE DATABASE") || batch.Contains("sys.databases"))
                {
                    effectiveConnectionString = GetMasterConnectionString(connectionString);
                    _logger.LogDebug("Using master database connection for database creation operations.");
                }

                using var connection = new SqlConnection(effectiveConnectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(batch, connection)
                {
                    CommandTimeout = 300 // 5 minutes timeout for database creation
                };

                await command.ExecuteNonQueryAsync();
                _logger.LogDebug("Executed SQL batch {BatchNumber} successfully.", i + 1);
            }
            catch (SqlException ex)
            {
                // Log but don't throw for certain expected errors
                if (ex.Number == 1801) // Database already exists
                {
                    _logger.LogDebug("Database already exists (SQL Error 1801) - continuing...");
                    continue;
                }
                if (ex.Number == 4060 && batch.Contains("CREATE DATABASE")) // Cannot open database (expected for creation)
                {
                    _logger.LogDebug("Database connection failed as expected during creation - continuing...");
                    continue;
                }

                _logger.LogError(ex, "SQL Error {Number}: {Message}", ex.Number, ex.Message);
                throw;
            }
        }
    }

    private static string GetMasterConnectionString(string originalConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(originalConnectionString);
        builder.InitialCatalog = "master";
        return builder.ConnectionString;
    }

    private static string GetDatabaseNameFromConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return builder.InitialCatalog ?? "AcadSyncAudit"; // Fallback to default name if not specified
    }

    private static List<string> SplitSqlScript(string sqlScript)
    {
        var batches = new List<string>();
        var lines = sqlScript.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var currentBatch = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Check if line is a GO statement (case-insensitive, standalone)
            if (string.Equals(trimmedLine, "GO", StringComparison.OrdinalIgnoreCase))
            {
                if (currentBatch.Count > 0)
                {
                    batches.Add(string.Join(Environment.NewLine, currentBatch));
                    currentBatch.Clear();
                }
            }
            else
            {
                currentBatch.Add(line);
            }
        }

        // Add the final batch if it exists
        if (currentBatch.Count > 0)
        {
            batches.Add(string.Join(Environment.NewLine, currentBatch));
        }

        return batches;
    }
}
