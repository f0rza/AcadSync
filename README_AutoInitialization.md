# AcadSync Database Auto-Initialization Feature

## Overview

The AcadSync application now includes automatic database initialization that runs on application startup. This feature ensures that the audit database and all required objects are created automatically when the application starts, eliminating the need for manual database setup.

## Features Implemented

### 1. DatabaseInitializationService
- **Location**: `AcadSync.Audit/Services/DatabaseInitializationService.cs`
- **Interface**: `IDatabaseInitializationService`
- **Purpose**: Handles automatic creation of audit database objects on application startup

### 2. Idempotent SQL Script
- **Location**: `AcadSync.Audit/SqlScripts/CreateAuditDatabase.sql`
- **Features**:
  - Safe to run multiple times (idempotent)
  - Creates database if it doesn't exist
  - Creates `acadsync` schema if it doesn't exist
  - Creates all required tables with existence checks:
    - `acadsync.ExtPropAudit` - Audit trail for extended property changes
    - `acadsync.ValidationRuns` - Track validation sessions
    - `acadsync.SystemLog` - Application logging
  - Creates reporting views:
    - `acadsync.vw_ExtPropAuditSummary` - Summarized audit data
    - `acadsync.vw_ValidationRunSummary` - Validation run statistics

### 3. Smart Connection Handling
- **Master Database Connection**: Automatically connects to `master` database for database creation operations
- **Target Database Connection**: Uses target database connection for schema and table operations
- **Dynamic Database Name**: Extracts database name from connection string (no hardcoded names)
- **Error Handling**: Gracefully handles expected SQL errors during database creation

### 4. Configuration Options
- **Auto-initialization Control**: `AcadSync:AutoInitializeDatabase` (default: true)
- **Connection String**: Uses `ConnectionStrings:AcadSyncAudit` from appsettings.json
- **Dynamic Database Name**: Database name comes from connection string, not hardcoded
- **Embedded Resource**: SQL script is embedded in the assembly for reliable deployment

## Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "AcadSyncAudit": "Server=localhost;Database=AcadSyncAudit;Integrated Security=true;TrustServerCertificate=true;"
  },
  "AcadSync": {
    "AutoInitializeDatabase": true
  }
}
```

### Dependency Injection
The service is automatically registered in `Program.cs`:
```csharp
services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();
```

## How It Works

1. **Application Startup**: When the application starts, the `DatabaseInitializationService` is called
2. **Configuration Check**: Verifies that auto-initialization is enabled and connection string exists
3. **Script Loading**: Loads the SQL script from embedded resource (with file system fallback)
4. **Batch Execution**: Splits the script by `GO` statements and executes each batch
5. **Smart Connection**: Uses master database for database creation, target database for other operations
6. **Error Handling**: Handles expected errors gracefully and logs all operations
7. **Completion**: Reports successful initialization and continues with application startup

## Database Objects Created

### Tables
- **acadsync.ExtPropAudit**: Stores audit trail of all extended property changes
- **acadsync.ValidationRuns**: Tracks validation sessions and their results
- **acadsync.SystemLog**: Application-level logging and diagnostics

### Views
- **acadsync.vw_ExtPropAuditSummary**: Aggregated audit data by date, rule, and severity
- **acadsync.vw_ValidationRunSummary**: Summary of validation runs with duration and status

### Indexes
- Optimized indexes on all tables for common query patterns
- Timestamp-based indexes for efficient date range queries
- Entity and rule-based indexes for fast lookups

## Benefits

1. **Zero Manual Setup**: Database objects are created automatically
2. **Deployment Friendly**: No separate database scripts to manage
3. **Idempotent**: Safe to run multiple times without errors
4. **Configurable**: Can be disabled if needed
5. **Robust Error Handling**: Gracefully handles various SQL Server scenarios
6. **Logging**: Full logging of initialization process for troubleshooting

## Testing

The feature has been tested and verified to:
- ✅ Create database from scratch when it doesn't exist
- ✅ Handle existing database scenarios gracefully
- ✅ Execute all SQL batches successfully
- ✅ Integrate seamlessly with application startup
- ✅ Provide detailed logging for troubleshooting

## Usage

The auto-initialization runs automatically when the application starts. No manual intervention is required. The initialization status is logged to the console and application logs:

```
🔧 Initializing database...
info: AcadSync.Audit.Services.DatabaseInitializationService[0]
      Starting database initialization...
info: AcadSync.Audit.Services.DatabaseInitializationService[0]
      Database initialization completed successfully.
```

To disable auto-initialization, set `AcadSync:AutoInitializeDatabase` to `false` in your configuration.
