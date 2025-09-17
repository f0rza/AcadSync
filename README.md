# AcadSync

Keep Anthology Student Extended Properties clean and compliant: scan, validate, and auto-repair with audit. YAML rules, simulate mode, and comprehensive logging. Built with .NET 8 for on-premises deployment.

## 🚀 Recent Updates

### 🔄 New Revert Feature (Latest)
AcadSync now supports reverting previous repair operations with comprehensive safety guards:

- **🔙 Safe Revert Operations**: Revert individual repairs or bulk operations with safety checks
- **🛡️ Safety Guards**: Verify current values match expected values before reverting
- **📋 Filter-Based Reverting**: Revert by time range, rule, entity, or validation run
- **🔍 Dry-Run Mode**: Preview what would be reverted without making changes
- **📝 Complete Audit Trail**: All revert operations are fully logged and auditable
- **⚡ Force Override**: Bypass safety checks when needed for emergency situations

**Usage:**
```bash
# Revert by runId (disregards date unless you also pass --from)
dotnet run revert 12345

# Revert by date (everything newer than the date)
dotnet run revert --from 2025-09-13

# Dry run (no changes; preview what would be reverted)
dotnet run revert --from 2025-09-13 --dry-run

# Force (override safety check mismatches)
dotnet run revert --from 2025-09-13 --force

# Combine runId and options
dotnet run revert 12345 --dry-run

# Verify audit DB schema (tables/columns)
dotnet run schema
```

**CLI Reference**
- Modes:
  - `demo`, `validate`, `simulate`, `repair`, `revert`, `schema`
- Revert options:
  - `runId` (number) - when provided, reverts by run; date filter is ignored unless also provided
  - `--from <yyyy-MM-dd | ISO>` - revert repairs newer than specified date
  - `--dry-run` or `-d` - preview actions without making changes
  - `--force` or `-f` - bypass safety checks when current DB values differ in formatting

### Major Processor Refactoring
The AcadSync.Processor has been completely refactored to improve maintainability, testability, and performance:

- **🏗️ Clean Architecture**: Modular design with clear separation of concerns
- **💉 Dependency Injection**: Full DI support with configurable services
- **⚡ Enhanced Performance**: Rule caching, parallel processing, and optimized data access
- **🔧 Better Configuration**: Strongly-typed configuration with environment support
- **📊 Rich Results**: Comprehensive validation and repair results with detailed statistics
- **🏥 Health Monitoring**: Built-in system health checks and diagnostics
- **🔄 Backward Compatibility**: Existing code continues to work without changes

**[📖 Read the full refactoring guide →](README_ProcessorRefactoring.md)**

## 🏛️ Architecture Overview

```
AcadSync/
├── AcadSync.App/              # Console application & entry point
│   ├── Program.cs            # Main application entry point
│   ├── appsettings.json      # Application configuration
│   ├── rules.yaml            # Business rules configuration
│   └── test-rules.yaml       # Test rules for development
├── AcadSync.Processor/        # Core validation & repair engine (REFACTORED ✨)
│   ├── Interfaces/           # Clean service contracts
│   │   ├── IValidationService.cs
│   │   ├── IRepairService.cs
│   │   ├── IRevertService.cs
│   │   └── IRuleEngine.cs
│   ├── Services/             # Modular, injectable services
│   │   ├── ValidationOrchestrator.cs
│   │   ├── RepairService.cs
│   │   ├── RevertService.cs
│   │   └── RuleEngine.cs
│   ├── Configuration/        # Strongly-typed settings
│   ├── Models/               # Domain and result models
│   ├── Repositories/         # Data access layer
│   ├── SqlScripts/          # Database initialization scripts
│   └── Utilities/           # Helper classes and utilities
├── AcadSync.Audit/           # Audit trail & compliance tracking
│   ├── Interfaces/          # Audit service contracts
│   ├── Repositories/        # Audit data access
│   ├── Services/            # Audit business logic
│   ├── Models/              # Audit data models
│   ├── Extensions/          # Extension methods
│   └── SqlScripts/          # Audit database schema
├── slnAcadSync.sln          # Visual Studio solution file
└── README files             # Comprehensive documentation
```

## ⚡ Quick Start

### 1. Basic Setup (New DI Approach)
```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Add AcadSync services with new architecture
builder.Services.AddAcadSyncProcessor(builder.Configuration, connectionString);
builder.Services.AddAcadSyncAudit(builder.Configuration);

var host = builder.Build();

// Use the services
var validationService = host.Services.GetRequiredService<IValidationService>();
var result = await validationService.ValidateAllAsync();

Console.WriteLine($"Found {result.Violations.Count} violations");
Console.WriteLine($"Processed {result.Summary.EntitiesProcessed} entities in {result.Duration.TotalSeconds:F1}s");
```

### 2. Configuration (appsettings.json)
```json
{
  "AcadSync": {
    "Processor": {
      "RulesFilePath": "rules.yaml",
      "ConnectionString": "Server=localhost;Database=AcadSync;Trusted_Connection=true;",
      "Cache": {
        "EnableRuleCache": true,
        "RuleCacheExpirationMinutes": 30
      },
      "Performance": {
        "EnableParallelProcessing": true,
        "MaxBatchSize": 1000
      }
    }
  }
}
```

### 3. Advanced Usage
```csharp
// System health check
var healthResult = await validationService.TestSystemHealthAsync();
Console.WriteLine($"System Status: {healthResult.OverallStatus}");

// Detailed validation with statistics
var detailedResult = await validationService.ValidateAllAsync(EprlMode.validate);
foreach (var (severity, count) in detailedResult.Summary.ViolationsBySeverity)
{
    Console.WriteLine($"{severity}: {count} violations");
}

// Repair with comprehensive tracking
var repairService = host.Services.GetRequiredService<IRepairService>();
var repairResult = await repairService.RepairViolationsAsync(violations, staffId: 1);
Console.WriteLine($"Repair Success Rate: {repairResult.SuccessRate:F1}%");
```

## 🎯 Key Features

### Core Validation Engine
- **📋 YAML Rule Definition**: Human-readable business rules
- **🔍 Multi-Entity Support**: Students, Documents, and extensible entity types
- **⚙️ Flexible Conditions**: Complex logical expressions with path resolution
- **🎭 Multiple Modes**: Validate, Simulate, and Repair operations

### New Architecture Benefits
- **🧪 Fully Testable**: All services are mockable with clear interfaces
- **📈 High Performance**: Intelligent caching and parallel processing
- **🔧 Configurable**: Environment-specific settings and feature toggles
- **📊 Rich Diagnostics**: Comprehensive results with detailed breakdowns
- **🏥 Health Monitoring**: Proactive system health checking

### Enterprise Features
- **📝 Complete Audit Trail**: Every change tracked with full context
- **🔒 Compliance Ready**: Detailed logging for regulatory requirements
- **🚀 Scalable Design**: Optimized for large datasets and high throughput
- **🔄 Backward Compatible**: Existing integrations continue to work

## 📚 Documentation

| Document | Description |
|----------|-------------|
| **[🏗️ Processor Refactoring Guide](README_ProcessorRefactoring.md)** | **Complete guide to the new architecture** |
| [🔍 Audit Extraction](README_AuditExtraction.md) | Audit trail and compliance features |
| [🗄️ Dual Database Setup](README_DualDatabase.md) | Multi-database configuration |
| [⚡ Auto Initialization](README_AutoInitialization.md) | Automated setup and deployment |

## 🛠️ Migration Guide

### For Existing Applications

**Option 1: Use New Services (Recommended)**
```csharp
// Old approach
var oldService = new ExtPropValidationService(repo, audit, logger);
var violations = await oldService.ValidateAllAsync();

// New approach
var validationService = serviceProvider.GetRequiredService<IValidationService>();
var result = await validationService.ValidateAllAsync();
var violations = result.Violations; // Same data structure
```

**Option 2: Backward-Compatible Facade**
```csharp
// Drop-in replacement - no code changes needed
var service = serviceProvider.GetRequiredService<RefactoredExtPropValidationService>();
var violations = await service.ValidateAllAsync(); // Same interface
```

## 🔧 Development Setup

### Prerequisites
- .NET 8.0 SDK
- SQL Server (LocalDB supported)
- Visual Studio 2022 or VS Code

### Build & Run
```bash
# Clone and build
git clone https://github.com/f0rza/AcadSync.git
cd AcadSync
dotnet build

# Run with new architecture
cd AcadSync.App
dotnet run
```

### Testing

This repository includes multiple test projects (unit and integration tests) that are part of the solution:

- `AcadSync.Processor.Tests`
- `AcadSync.Audit.Tests`
- `AcadSync.App.Tests`

Run all tests for the solution:
```bash
dotnet test
```

Run a specific test project:
```bash
dotnet test AcadSync.Processor.Tests/AcadSync.Processor.Tests.csproj
```

Run only integration tests (tests marked with `Category=Integration`):
```bash
dotnet test --filter "Category=Integration"
```

Run tests with code coverage (XPlat collector):
```bash
dotnet test --collect:"XPlat Code Coverage"
```

Notes:
- Tests in this repository use MSTest (see `MSTestSettings` files in the test projects).
- Test results and coverage outputs are written under each test project's `TestResults/` directory by default.
- To run tests from within Visual Studio, open `slnAcadSync.sln` and run Test Explorer.

## 🚀 Future Plans / Roadmap

Planned improvements and upcoming work include:

- **Add new validation rules** via YAML configuration to support additional business scenarios
- **Implement custom repositories** to support alternative data sources and providers  
- **Create custom, pluggable repair strategies** for safer and configurable automated fixes
- **Extend health monitoring** with more system checks and proactive alerting

## 🆘 Support

- **📖 Documentation**: Comprehensive guides in the repository
- **🐛 Issues**: Report bugs via GitHub Issues
- **💡 Feature Requests**: Suggest improvements via GitHub Discussions
- **📧 Enterprise Support**: Contact for commercial licensing and support

---

**⭐ Star this repository if AcadSync helps keep your Anthology Student data clean and compliant!**
