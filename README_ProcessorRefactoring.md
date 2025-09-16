# AcadSync.Processor Refactoring

This document describes the comprehensive refactoring of the AcadSync.Processor project to improve maintainability, testability, and separation of concerns.

## Overview

The refactoring transforms the monolithic `ExtPropValidationService` into a clean, modular architecture following SOLID principles and dependency injection patterns.

## Architecture Changes

### Before (Original Architecture)
- **Static Dependencies**: `Evaluator` class used static methods and ThreadStatic fields
- **Monolithic Service**: `ExtPropValidationService` handled too many responsibilities
- **Hard-coded Dependencies**: File paths and connection logic embedded in services
- **Mixed Concerns**: Business logic mixed with data access patterns
- **Limited Configuration**: No centralized configuration management

### After (Refactored Architecture)
- **Dependency Injection Ready**: All services are injectable with proper lifetime management
- **Single Responsibility**: Each service has a focused, well-defined purpose
- **Configuration Management**: Centralized, strongly-typed configuration
- **Clean Interfaces**: Clear boundaries between components
- **Enhanced Error Handling**: Structured error handling with detailed results

## New Project Structure

```
AcadSync.Processor/
├── Configuration/
│   └── ProcessorOptions.cs              # Strongly-typed configuration
├── Extensions/
│   └── ServiceCollectionExtensions.cs   # DI registration extensions
├── Interfaces/
│   ├── IRuleEngine.cs                   # Core rule evaluation
│   ├── IRuleLoader.cs                   # Rule loading and caching
│   ├── IEntityService.cs                # Entity data retrieval
│   ├── IValidationService.cs            # Main orchestration
│   ├── IRepairService.cs                # Violation repair logic
│   └── IRevertService.cs                # Repair revert operations
├── Models/
│   └── Results/                         # Result models
│       ├── ValidationResult.cs
│       ├── RepairResult.cs
│       └── SystemHealthResult.cs
└── Services/
    ├── RuleEngine.cs                    # Non-static rule evaluator
    ├── FileSystemRuleLoader.cs          # YAML rule loading with caching
    ├── EntityService.cs                 # Entity retrieval orchestration
    ├── ValidationOrchestrator.cs        # Main validation workflow
    ├── RepairService.cs                 # Violation repair logic
    ├── RevertService.cs                 # Repair revert operations
    └── RefactoredExtPropValidationService.cs  # Backward-compatible facade
```

## Key Components

### 1. IRuleEngine & RuleEngine
**Purpose**: Pure rule evaluation logic without static dependencies

**Key Features**:
- Non-static, injectable implementation
- Async/await throughout
- Proper logging integration
- Thread-safe operation

**Usage**:
```csharp
var violations = await ruleEngine.EvaluateAsync(ruleDoc, entities);
```

### 2. IRuleLoader & FileSystemRuleLoader
**Purpose**: Rule loading with intelligent caching

**Key Features**:
- File-based rule loading with cache invalidation
- YAML parsing with validation
- Configurable cache expiration
- File modification detection

**Usage**:
```csharp
var ruleDoc = await ruleLoader.LoadRulesAsync();
```

### 3. IEntityService & EntityService
**Purpose**: Entity data retrieval orchestration

**Key Features**:
- Parallel data loading
- Detailed logging with breakdowns
- Connection testing
- Filtering support

**Usage**:
```csharp
var entities = await entityService.GetAllEntitiesAsync();
```

### 4. IValidationService & ValidationOrchestrator
**Purpose**: Main validation workflow orchestration

**Key Features**:
- Comprehensive validation results
- System health checking
- Custom rule file support
- Detailed statistics and summaries

**Usage**:
```csharp
var result = await validationService.ValidateAllAsync(EprlMode.validate);
```

### 5. IRepairService & RepairService
**Purpose**: Violation repair with audit trail

**Key Features**:
- Selective repair based on business rules
- Comprehensive audit logging
- Detailed success/failure tracking
- Combined validation and repair operations

**Usage**:
```csharp
var repairResult = await repairService.RepairViolationsAsync(violations, staffId);
```

### 6. IRevertService & RevertService
**Purpose**: Safe reversion of previous repair operations with comprehensive safety guards

**Key Features**:
- Filter-based revert operations (time range, rule, entity, run ID)
- Safety checks to verify current values match expected values
- Dry-run mode for previewing changes
- Force override for emergency situations
- Complete audit trail for all revert operations
- Detailed success/failure tracking

**Usage**:
```csharp
// Revert repairs from the last hour
var result = await revertService.RevertByFilterAsync(
    from: DateTimeOffset.UtcNow.AddHours(-1),
    force: false,
    staffId: 1,
    dryRun: false);

// Revert specific repairs
var result = await revertService.RevertAsync(repairs, force: false, staffId: 1);
```

## Configuration

### appsettings.json Example
```json
{
  "AcadSync": {
    "Processor": {
      "RulesFilePath": "rules.yaml",
      "ConnectionString": "Server=localhost;Database=AcadSync;Trusted_Connection=true;",
      "DefaultStaffId": 1,
      "Cache": {
        "EnableRuleCache": true,
        "RuleCacheExpirationMinutes": 30,
        "EnablePropertyCache": true,
        "PropertyCacheExpirationMinutes": 60
      },
      "Logging": {
        "EnableStructuredLogging": true,
        "LogViolationDetails": true,
        "LogPerformanceMetrics": true,
        "MinimumLogLevel": "Information"
      },
      "Performance": {
        "CommandTimeoutSeconds": 30,
        "MaxBatchSize": 1000,
        "EnableParallelProcessing": true,
        "MaxDegreeOfParallelism": 4
      }
    }
  }
}
```

## Dependency Injection Setup

### Program.cs / Startup.cs
```csharp
// Basic setup
services.AddAcadSyncProcessor(configuration, connectionString);

// With custom repository
services.AddAcadSyncProcessor<CustomRepository>(configuration);

// Manual configuration
services.AddAcadSyncProcessor(options =>
{
    options.ConnectionString = "your-connection-string";
    options.RulesFilePath = "custom-rules.yaml";
    options.DefaultStaffId = 1;
});

// Validation only (no repair functionality)
services.AddAcadSyncValidation(configuration, connectionString);
```

## Migration Guide

### For Existing Code Using ExtPropValidationService

**Before**:
```csharp
var service = new ExtPropValidationService(repository, auditRepository, logger);
var violations = await service.ValidateAllAsync();
```

**After (Option 1 - Use new services directly)**:
```csharp
// Inject services
public MyController(IValidationService validationService)
{
    _validationService = validationService;
}

// Use in methods
var result = await _validationService.ValidateAllAsync();
var violations = result.Violations;
```

**After (Option 2 - Use backward-compatible facade)**:
```csharp
// Inject the refactored service
public MyController(RefactoredExtPropValidationService service)
{
    _service = service;
}

// Same interface as before
var violations = await _service.ValidateAllAsync();
```

## Benefits of Refactoring

### 1. **Improved Testability**
- All dependencies are mockable
- Clear interface boundaries
- Focused unit test scope per service

### 2. **Better Performance**
- Rule caching to avoid repeated YAML parsing
- Parallel entity loading
- Configurable batch processing

### 3. **Enhanced Maintainability**
- Single Responsibility Principle
- Dependency Injection pattern
- Centralized configuration

### 4. **Robust Error Handling**
- Structured result objects
- Comprehensive error information
- Audit trail for all operations

### 5. **Flexible Configuration**
- Environment-specific settings
- Runtime configuration changes
- Feature toggles

## Advanced Features

### System Health Monitoring
```csharp
var healthResult = await validationService.TestSystemHealthAsync();
Console.WriteLine($"Overall Status: {healthResult.OverallStatus}");
Console.WriteLine($"Summary: {healthResult.GetSummary()}");

foreach (var component in healthResult.Components)
{
    Console.WriteLine($"{component.Name}: {component.Status} ({component.ResponseTime}ms)");
}
```

### Detailed Validation Results
```csharp
var result = await validationService.ValidateAllAsync(EprlMode.validate);

Console.WriteLine($"Processed {result.Summary.EntitiesProcessed} entities");
Console.WriteLine($"Found {result.Summary.TotalViolations} violations");
Console.WriteLine($"Success Rate: {result.Summary.RepairableViolations}/{result.Summary.TotalViolations} repairable");

// Breakdown by severity
foreach (var (severity, count) in result.Summary.ViolationsBySeverity)
{
    Console.WriteLine($"  {severity}: {count}");
}
```

### Custom Rule Files
```csharp
var result = await validationService.ValidateWithCustomRulesAsync("test-rules.yaml");
```

## Performance Considerations

### Caching Strategy
- **Rule Cache**: Avoids repeated YAML parsing
- **Property Cache**: Reduces database queries for property definitions
- **File Modification Detection**: Automatic cache invalidation

### Parallel Processing
- **Entity Loading**: Students and documents loaded in parallel
- **Configurable Parallelism**: Adjustable based on system resources
- **Batch Processing**: Large datasets processed in configurable batches

### Connection Management
- **Connection Pooling**: Leverages built-in SQL connection pooling
- **Timeout Configuration**: Configurable command timeouts
- **Health Monitoring**: Proactive connection health checking

## Backward Compatibility

The refactoring maintains backward compatibility through:

1. **RefactoredExtPropValidationService**: Drop-in replacement for the original service
2. **Same Method Signatures**: Existing code can use the new service without changes
3. **Result Compatibility**: Returns the same data structures as before

## Future Enhancements

### Planned Improvements
1. **Metrics and Monitoring**: Integration with application metrics
2. **Distributed Caching**: Redis support for multi-instance deployments
3. **Rule Versioning**: Support for rule version management
4. **Custom Validators**: Plugin architecture for custom validation logic
5. **Performance Profiling**: Built-in performance analysis tools

### Extension Points
- **Custom Repositories**: Easy to implement custom data sources
- **Custom Rule Loaders**: Support for database-stored rules
- **Custom Repair Strategies**: Pluggable repair logic
- **Custom Health Checks**: Additional system health components

## Conclusion

This refactoring transforms the AcadSync.Processor from a monolithic service into a modern, maintainable, and testable architecture. The new design follows industry best practices while maintaining full backward compatibility for existing applications.

The modular approach allows for easier testing, better performance, and more flexible deployment scenarios, setting the foundation for future enhancements and scalability improvements.
