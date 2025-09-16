# AcadSync Dual-Database Architecture

## Overview

AcadSync now uses a dual-database architecture to ensure complete separation between read operations (Anthology Student) and audit operations (AcadSync Audit). This design provides:

- ✅ **Zero impact** on Anthology Student database
- ✅ **Dirty reads** (NOLOCK) to prevent blocking Anthology operations
- ✅ **Separate audit database** for compliance and retention
- ✅ **Independent scaling** and backup strategies
- ✅ **Maintains Anthology support** compliance

## Architecture

```
┌─────────────────────┐    ┌─────────────────────┐
│   Anthology Student │    │   AcadSync Audit    │
│     (Read-Only)     │    │   (Read/Write)      │
├─────────────────────┤    ├─────────────────────┤
│ • Student data      │    │ • acadsync schema   │
│ • Document data     │    │ • ExtPropAudit      │
│ • Extended props    │    │ • ValidationRuns    │
│ • WITH (NOLOCK)     │    │ • SystemLog         │
└─────────────────────┘    └─────────────────────┘
           │                           │
           └─────────┬─────────────────┘
                     │
           ┌─────────────────────┐
           │    AcadSync App     │
           │                     │
           │ • AnthologyRepo     │
           │ • AuditRepository   │
           │ • ValidationService │
           └─────────────────────┘
```

## Database Setup

### 1. Create AcadSync Audit Database

Run the following SQL scripts in order:

```sql
-- 1. Create database and schema
.\AcadSync.Processor\SqlScripts\CreateAcadSyncDatabase.sql

-- 2. Create audit tables
.\AcadSync.Processor\SqlScripts\CreateAuditTable.sql
```

### 2. Configure Connection Strings

Update `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AnthologyStudent": "Server=your-anthology-server;Database=AnthologyStudent;Integrated Security=true;TrustServerCertificate=true;",
    "AcadSyncAudit": "Server=your-audit-server;Database=AcadSyncAudit;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

## Database Schema

### AcadSync Audit Database (acadsync schema)

#### acadsync.ExtPropAudit
- **Purpose**: Track all extended property violations and repairs
- **Key Fields**: RuleId, EntityType, EntityId, PropertyCode, OldValue, NewValue, Action, Severity
- **Indexes**: Optimized for time-based queries and entity lookups

#### acadsync.ValidationRuns
- **Purpose**: Track validation sessions and performance metrics
- **Key Fields**: StartTime, EndTime, Mode, ViolationCount, RepairedCount, Status
- **Use Cases**: Performance monitoring, audit trails, scheduling

#### acadsync.SystemLog
- **Purpose**: Application logging and error tracking
- **Key Fields**: Timestamp, Level, Message, Exception, Source
- **Use Cases**: Debugging, monitoring, alerting

### Views for Reporting

#### acadsync.vw_ExtPropAuditSummary
- Daily violation summaries by rule, entity type, and severity
- Repair success rates and trends

#### acadsync.vw_ValidationRunSummary
- Validation run performance and outcomes
- Duration tracking and success rates

## Key Features

### Read Operations (Anthology)
- All queries use `WITH (NOLOCK)` for dirty reads
- No blocking of Anthology operations
- Cached property ID lookups for performance
- Read-only access pattern

### Write Operations (Audit)
- All audit data written to separate database
- Comprehensive violation tracking
- Performance metrics collection
- Configurable retention policies

### Configuration Options

```json
{
  "AcadSync": {
    "DefaultStaffId": 1,
    "MaxConcurrentOperations": 10,
    "EnableAuditLogging": true,
    "RulesFile": "rules.yaml",
    "AuditRetentionDays": 365,
    "UseReadUncommitted": true
  }
}
```

## Usage Examples

### Demo Mode (No Database Required)
```bash
dotnet run demo
```

### Validation Mode (Requires Both Databases)
```bash
dotnet run validate    # Read-only validation
dotnet run simulate    # Show proposed repairs
dotnet run repair      # Apply repairs with audit
```

## Monitoring and Maintenance

### Audit Data Cleanup
The system includes automatic cleanup of old audit records based on retention policy:

```csharp
await auditRepository.CleanupOldAuditRecordsAsync(365); // Keep 1 year
```

### Performance Monitoring
Use the validation run views to monitor system performance:

```sql
SELECT * FROM acadsync.vw_ValidationRunSummary 
WHERE StartTime >= DATEADD(day, -7, GETDATE())
ORDER BY StartTime DESC;
```

### Violation Trending
Track violation patterns over time:

```sql
SELECT * FROM acadsync.vw_ExtPropAuditSummary 
WHERE AuditDate >= DATEADD(day, -30, GETDATE())
ORDER BY AuditDate DESC, ViolationCount DESC;
```

## Security Considerations

1. **Separate Service Accounts**: Use different SQL accounts for Anthology (read-only) and Audit (read/write)
2. **Network Isolation**: Consider placing audit database on separate server/network
3. **Backup Strategy**: Independent backup schedules for each database
4. **Retention Policies**: Implement appropriate data retention for compliance

## Troubleshooting

### Connection Issues
- Verify both connection strings are correct
- Test connectivity to both databases
- Check SQL Server authentication/permissions

### Performance Issues
- Monitor dirty read impact on Anthology
- Review audit database sizing and indexing
- Consider audit database placement (separate server)

### Audit Data Growth
- Monitor audit table sizes
- Implement retention policies
- Consider partitioning for large datasets

## Migration from Single Database

If migrating from a previous single-database setup:

1. Create new AcadSync audit database
2. Update connection strings
3. Migrate existing audit data (if any)
4. Update application configuration
5. Test thoroughly in non-production environment

This dual-database architecture ensures AcadSync operates safely alongside Anthology Student while providing comprehensive audit capabilities.
