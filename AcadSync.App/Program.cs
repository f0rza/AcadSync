using AcadSync.Processor;
using AcadSync.Audit.Interfaces;
using AcadSync.Audit.Repositories;
using AcadSync.Audit.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Build host with DI
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IConfiguration>(configuration);
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
            var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "demo";
            
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

    static void ShowUsage()
    {
        Console.WriteLine("Usage: dotnet run [mode]");
        Console.WriteLine();
        Console.WriteLine("Modes:");
        Console.WriteLine("  demo      - Run with sample data (default)");
        Console.WriteLine("  validate  - Validate against real database");
        Console.WriteLine("  simulate  - Show what repairs would be made");
        Console.WriteLine("  repair    - Apply repairs to database");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  Set connection string in appsettings.json:");
        Console.WriteLine("  \"ConnectionStrings\": {");
        Console.WriteLine("    \"AnthologyStudent\": \"Server=...;Database=...;\"");
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
