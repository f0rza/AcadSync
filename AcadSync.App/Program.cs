using AcadSync.Processor;
using AcadSync.Processor.Services;
using AcadSync.Processor.Extensions;
using AcadSync.Processor.Models.Domain;
using AcadSync.Processor.Models.Projections;
using AcadSync.Processor.Utilities;
using AcadSync.Audit.Interfaces;
using AcadSync.Audit.Repositories;
using AcadSync.Audit.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace AcadSync.App;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🎓 AcadSync - Extended Property Sync & Validator");
        Console.WriteLine("================================================");
        Console.WriteLine();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Build host with DI
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IConfiguration>(configuration);

                // Add AcadSync Processor services using extension method
                services.AddAcadSyncProcessor(configuration);

                // Override repository registrations with custom connection strings
                services.AddScoped<IExtPropRepository>(provider =>
                {
                    var connectionString = configuration.GetConnectionString("AnthologyStudent")
                        ?? "Server=localhost;Database=AnthologyStudent;Integrated Security=true;TrustServerCertificate=true;";
                    return new AnthologyExtPropRepository(connectionString);
                });
                services.AddScoped<IAuditRepository>(provider =>
                {
                    var connectionString = configuration.GetConnectionString("AcadSyncAudit")
                        ?? "Server=localhost;Database=AcadSyncAudit;Integrated Security=true;TrustServerCertificate=true;";
                    return new AuditRepository(connectionString);
                });

                services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();
                services.AddScoped<ExtPropValidationService>();
            })
            .Build();

        try
        {
            using var scope = host.Services.CreateScope();
            
            // Initialize database on startup
            Console.WriteLine("🔧 Initializing database...");
            var dbInitService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
            await dbInitService.InitializeAsync();
            Console.WriteLine();
            
            var validationService = scope.ServiceProvider.GetRequiredService<ExtPropValidationService>();

            // Parse command line arguments
            var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "revert"; // todo: revert - "demo";

            switch (mode)
            {
                case "demo":
                    await RunDemoAsync(validationService);
                    break;
                case "validate":
                    await RunValidationAsync(validationService, simulate: false);
                    break;
                case "simulate":
                    await RunValidationAsync(validationService, simulate: true);
                    break;
                case "repair":
                    await RunRepairAsync(validationService);
                    break;
                case "revert":
                    // Supported syntaxes:
                    // dotnet run revert [runId] [--from yyyy-MM-dd|ISO] [--force|-f] [--dry-run|-d]
                    long? runId = null;
                    DateTimeOffset? fromDate = null;
                    bool force = false;
                    bool dryRun = false;

                    // Parse args starting from index 1
                    for (int i = 1; i < args.Length; i++)
                    {
                        var arg = args[i];

                        if (long.TryParse(arg, out var parsedRunId))
                        {
                            runId = parsedRunId;
                            continue;
                        }

                        var lower = arg.ToLowerInvariant();
                        if (lower == "--force" || lower == "-f")
                        {
                            force = true;
                            continue;
                        }
                        if (lower == "--dry-run" || lower == "-d")
                        {
                            dryRun = true;
                            continue;
                        }

                        // --from=VALUE or --from VALUE or standalone date literal
                        if (lower.StartsWith("--from="))
                        {
                            var value = arg.Substring("--from=".Length);
                            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                                fromDate = dto;
                            else if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                                fromDate = new DateTimeOffset(dt, TimeSpan.Zero);
                            continue;
                        }
                        if (lower == "--from" && i + 1 < args.Length)
                        {
                            var value = args[++i];
                            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                                fromDate = dto;
                            else if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                                fromDate = new DateTimeOffset(dt, TimeSpan.Zero);
                            continue;
                        }

                        // As a convenience: try to parse any standalone ISO-like date
                        if (DateTimeOffset.TryParse(arg, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto2))
                        {
                            fromDate = dto2;
                            continue;
                        }
                        if (DateTime.TryParse(arg, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt2))
                        {
                            fromDate = new DateTimeOffset(dt2, TimeSpan.Zero);
                            continue;
                        }
                    }

                    await RunRevertAsync(validationService, runId, fromDate, force, dryRun);
                    break;
                case "schema":
                    await RunSchemaCheckAsync(configuration);
                    break;
                default:
                    ShowUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }

    static async Task RunDemoAsync(ExtPropValidationService service)
    {
        Console.WriteLine("🚀 Running Demo with Sample Data");
        Console.WriteLine("================================");
        Console.WriteLine();

        // Create sample data (simulating what would come from Anthology database)
        var sampleStudents = CreateSampleStudents();
        var sampleDocuments = CreateSampleDocuments();

        Console.WriteLine($"📊 Sample Data Created:");
        Console.WriteLine($"   • {sampleStudents.Count} students");
        Console.WriteLine($"   • {sampleDocuments.Count} documents");
        Console.WriteLine();

        // Load and validate rules
        var rulesPath = Path.Combine(Directory.GetCurrentDirectory(), "test-rules.yaml");
        if (!File.Exists(rulesPath))
        {
            Console.WriteLine($"❌ Rules file not found: {rulesPath}");
            return;
        }

        var yamlContent = await File.ReadAllTextAsync(rulesPath);
        var doc = EprlLoader.LoadFromYaml(yamlContent);

        Console.WriteLine($"📋 Loaded Rules: {doc.Ruleset.Name} v{doc.Ruleset.Version}");
        Console.WriteLine($"   • {doc.Rules.Count} rules defined");
        Console.WriteLine($"   • Tenant: {doc.Tenant}");
        Console.WriteLine();

        // Evaluate rules
        var allEntities = new List<IEntityProjection>();
        allEntities.AddRange(sampleStudents);
        allEntities.AddRange(sampleDocuments);

        var violations = Evaluator.EvaluateWithContext(doc, allEntities).ToList();

        // Display results
        Console.WriteLine($"🔍 Validation Results:");
        Console.WriteLine($"   • {violations.Count} violations found");
        Console.WriteLine();

        if (violations.Any())
        {
            Console.WriteLine("📝 Violation Details:");
            Console.WriteLine("====================");

            foreach (var violation in violations.Take(10)) // Show first 10
            {
                var icon = violation.Severity switch
                {
                    Severity.block => "🚫",
                    Severity.error => "❌",
                    Severity.warning => "⚠️",
                    _ => "ℹ️"
                };

                Console.WriteLine($"{icon} {violation.EntityType}#{violation.EntityId} - {violation.PropertyCode}");
                Console.WriteLine($"   Rule: {violation.RuleId}");
                Console.WriteLine($"   Issue: {violation.Reason}");
                Console.WriteLine($"   Current: '{violation.CurrentValue}' → Proposed: '{violation.ProposedValue}'");
                Console.WriteLine($"   Action: {violation.Action}");
                Console.WriteLine();
            }

            if (violations.Count > 10)
            {
                Console.WriteLine($"... and {violations.Count - 10} more violations");
                Console.WriteLine();
            }

            // Summary by severity
            var summary = violations.GroupBy(v => v.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            Console.WriteLine("📊 Summary by Severity:");
            foreach (var (severity, count) in summary.OrderByDescending(x => x.Key))
            {
                var icon = severity switch
                {
                    Severity.block => "🚫",
                    Severity.error => "❌",
                    Severity.warning => "⚠️",
                    _ => "ℹ️"
                };
                Console.WriteLine($"   {icon} {severity}: {count}");
            }
        }
        else
        {
            Console.WriteLine("✅ No violations found - all extended properties are compliant!");
        }

        Console.WriteLine();
        Console.WriteLine("💡 Next Steps:");
        Console.WriteLine("   • Run 'dotnet run simulate' to see what repairs would be made");
        Console.WriteLine("   • Run 'dotnet run repair' to apply fixes (requires database connection)");
        Console.WriteLine("   • Customize rules.yaml for your institution's requirements");
    }

    static async Task RunValidationAsync(ExtPropValidationService service, bool simulate)
    {
        var mode = simulate ? "Simulation" : "Validation";
        Console.WriteLine($"🔍 Running {mode} Mode");
        Console.WriteLine("======================");
        Console.WriteLine();

        try
        {
            var results = await service.ValidateAllAsync(simulate ? EprlMode.simulate : EprlMode.validate);

            Console.WriteLine($"📊 {mode} Complete:");
            Console.WriteLine($"   • {results.Count} violations found");

            if (results.Any())
            {
                var summary = results.GroupBy(v => v.Severity)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var (severity, count) in summary.OrderByDescending(x => x.Key))
                {
                    Console.WriteLine($"   • {severity}: {count}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ {mode} failed: {ex.Message}");
            Console.WriteLine("💡 Make sure your database connection string is configured in appsettings.json");
        }
    }

    static async Task RunRepairAsync(ExtPropValidationService service)
    {
        Console.WriteLine("🔧 Running Repair Mode");
        Console.WriteLine("=====================");
        Console.WriteLine();

        Console.WriteLine("⚠️  WARNING: This will modify your Anthology Student database!");
        Console.Write("Are you sure you want to continue? (y/N): ");

        var response = Console.ReadLine()?.ToLowerInvariant();
        if (response != "y" && response != "yes")
        {
            Console.WriteLine("❌ Repair cancelled by user");
            return;
        }

        try
        {
            var results = await service.ValidateAndRepairAsync();

            Console.WriteLine($"🔧 Repair Complete:");
            Console.WriteLine($"   • {results.Count} violations processed");
            Console.WriteLine($"   • Check ExtPropAudit table for detailed logs");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Repair failed: {ex.Message}");
        }
    }

    static async Task RunRevertAsync(ExtPropValidationService service, long? runId, DateTimeOffset? fromDate, bool force, bool dryRun)
    {
        Console.WriteLine("🔄 Running Revert Mode");
        Console.WriteLine("=====================");
        Console.WriteLine();

        if (runId.HasValue)
            Console.WriteLine($"🎯 Target: RunId #{runId.Value}");
        if (fromDate.HasValue)
            Console.WriteLine($"🗓️  From: {fromDate.Value:yyyy-MM-dd HH:mm:ss zzz}");
        Console.WriteLine($"🔧 Force: {(force ? "ENABLED" : "disabled")}, DryRun: {(dryRun ? "ENABLED" : "disabled")}");
        Console.WriteLine();

        if (!dryRun)
        {
            Console.WriteLine("⚠️  WARNING: This will revert previous repair operations!");
            Console.Write("Are you sure you want to continue? (y/N): ");
            var response = Console.ReadLine()?.ToLowerInvariant();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("❌ Revert cancelled by user");
                return;
            }
        }

        try
        {
            var staffId = 1; // Default; can be wired from config if needed

            AcadSync.Processor.Models.Results.RepairResult results;
            if (runId.HasValue)
            {
                // If runId is specified, prefer it and disregard date unless explicitly intended downstream
                results = await service.RevertByRunIdAsync(runId.Value, force, staffId, dryRun);
            }
            else if (fromDate.HasValue)
            {
                results = await service.RevertByDateRangeAsync(fromDate.Value, null, force, staffId, dryRun);
            }
            else
            {
                // Fallback: last hour
                var fallback = DateTimeOffset.UtcNow.AddHours(-1);
                results = await service.RevertRepairsAsync(fallback, force, staffId, dryRun);
            }

            Console.WriteLine($"🔄 Revert Complete:");
            Console.WriteLine($"   • {results.SuccessfulRepairs} repairs reverted successfully");
            Console.WriteLine($"   • {results.FailedRepairs} reverts failed");
            Console.WriteLine($"   • Check ExtPropAudit table for detailed logs");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Revert failed: {ex.Message}");
        }
    }

    static async Task RunSchemaCheckAsync(IConfiguration configuration)
    {
        Console.WriteLine("🔎 Checking AcadSyncAudit schema for ExtPropAudit / ValidationRuns / SystemLog...");
        var cs = configuration.GetConnectionString("AcadSyncAudit");
        if (string.IsNullOrWhiteSpace(cs))
        {
            Console.WriteLine("❌ AcadSyncAudit connection string is missing.");
            return;
        }

        try
        {
            using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            // Check tables exist
            var tables = new Dictionary<string, string>
            {
                { "acadsync.ExtPropAudit", @"
SELECT 1 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'acadsync' AND TABLE_NAME = 'ExtPropAudit';" },
                { "acadsync.ValidationRuns", @"
SELECT 1 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'acadsync' AND TABLE_NAME = 'ValidationRuns';" },
                { "acadsync.SystemLog", @"
SELECT 1 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'acadsync' AND TABLE_NAME = 'SystemLog';" }
            };

            foreach (var kv in tables)
            {
                using var cmd = new SqlCommand(kv.Value, conn);
                var exists = await cmd.ExecuteScalarAsync();
                Console.WriteLine(exists != null ? $"✅ Table exists: {kv.Key}" : $"❌ Table missing: {kv.Key}");
            }

            // Check ExtPropAudit required columns
            var requiredColumns = new[]
            {
                "AuditId","Timestamp","RuleId","EntityType","EntityId","PropertyCode",
                "OldValue","NewValue","Action","Severity","Operator","RunId","Notes"
            };

            var colSql = @"
SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='acadsync' AND TABLE_NAME='ExtPropAudit';";

            using var colCmd = new SqlCommand(colSql, conn);
            using var reader = await colCmd.ExecuteReaderAsync();
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync())
            {
                cols.Add(reader.GetString(0));
            }

            Console.WriteLine();
            Console.WriteLine("ExtPropAudit column check:");
            foreach (var col in requiredColumns)
            {
                Console.WriteLine(cols.Contains(col) ? $"  ✅ {col}" : $"  ❌ {col} (missing)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Schema check failed: {ex.Message}");
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage: dotnet run [mode]");
        Console.WriteLine();
        Console.WriteLine("Modes:");
        Console.WriteLine("  demo      - Run with sample data (default)");
        Console.WriteLine("  validate  - Validate against real database");
        Console.WriteLine("  simulate  - Show what repairs would be made");
        Console.WriteLine("  repair    - Apply repairs to database");
        Console.WriteLine("  revert    - Revert previous repair operations");
        Console.WriteLine();
        Console.WriteLine("Revert options:");
        Console.WriteLine("  revert [runId] [--from yyyy-MM-dd|ISO] [--force|-f] [--dry-run|-d]");
        Console.WriteLine("  Examples:");
        Console.WriteLine("    dotnet run revert 1                # revert by runId");
        Console.WriteLine("    dotnet run revert --from 2025-09-13 --dry-run");
        Console.WriteLine();
        Console.WriteLine("Tools:");
        Console.WriteLine("  schema    - Verify audit DB schema (tables/columns)");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  Set connection string in appsettings.json:");
        Console.WriteLine("  \"ConnectionStrings\": {");
        Console.WriteLine("    \"AnthologyStudent\": \"Server=...;Database=...;\"");
        Console.WriteLine("    \"AcadSyncAudit\": \"Server=...;Database=...;\"");
        Console.WriteLine("  }");
    }

    static List<StudentProjection> CreateSampleStudents()
    {
        return new List<StudentProjection>
        {
            // Nursing student with missing immunization
            new StudentProjection(
                id: 24120,
                studentNumber: "2200130657",
                programCode: "NURS",
                status: "Active",
                campus: "MAIN",
                citizenship: "Domestic",
                visaType: null,
                country: "CA",
                documents: new List<DocumentItem>
                {
                    new("IMM", new Dictionary<string, object?> { ["ExpiryDate"] = "2024-09-01" }) // expired!
                },
                ext: new Dictionary<string, string?>
                {
                    ["InternationalFlag"] = "false"
                    // Missing: ImmunizationExpiryDate, ClinicalClearanceStatus
                }
            ),

            // International student with incorrect flag
            new StudentProjection(
                id: 24121,
                studentNumber: "2200130658",
                programCode: "BUSI",
                status: "Active",
                campus: "MAIN",
                citizenship: "International",
                visaType: "StudyPermit",
                country: "IN",
                documents: new List<DocumentItem>(),
                ext: new Dictionary<string, string?>
                {
                    ["InternationalFlag"] = "N" // should be true!
                    // Missing: StudyPermitExpiry
                }
            ),

            // Canadian student missing tax info
            new StudentProjection(
                id: 24122,
                studentNumber: "2200130659",
                programCode: "ARTS",
                status: "Active",
                campus: "MAIN",
                citizenship: "Domestic",
                visaType: null,
                country: "CA",
                documents: new List<DocumentItem>(),
                ext: new Dictionary<string, string?>
                {
                    ["InternationalFlag"] = "false"
                    // Missing: ProvinceCode, PSTExempt
                }
            )
        };
    }

    static List<DocumentProjection> CreateSampleDocuments()
    {
        return new List<DocumentProjection>
        {
            // OEPP document missing hold link
            new DocumentProjection(
                id: 5555,
                documentTypeCode: "OEPP561",
                meta: new Dictionary<string, object?> { ["SomeKey"] = "val" },
                ext: new Dictionary<string, string?>
                {
                    // Missing: RelatedHoldCode
                }
            ),

            // Official transcript missing verification
            new DocumentProjection(
                id: 5556,
                documentTypeCode: "TRANSCRIPT_OFFICIAL",
                meta: new Dictionary<string, object?>(),
                ext: new Dictionary<string, string?>
                {
                    // Missing: VerificationStatus
                }
            )
        };
    }
}
