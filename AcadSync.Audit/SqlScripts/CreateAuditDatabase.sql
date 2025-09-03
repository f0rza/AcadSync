-- AcadSync Audit Tables Creation Script
-- This script can be run multiple times safely - it checks for existence before creating objects
-- Auto-executed on application startup if objects don't exist

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AcadSyncAudit')
BEGIN
    CREATE DATABASE AcadSyncAudit
    COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'Created AcadSyncAudit database.';
END
ELSE
BEGIN
    PRINT 'AcadSyncAudit database already exists.';
END
GO

USE AcadSyncAudit;
GO

-- Create acadsync schema if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'acadsync')
BEGIN
    EXEC('CREATE SCHEMA acadsync');
    PRINT 'Created acadsync schema.';
END
ELSE
BEGIN
    PRINT 'acadsync schema already exists.';
END
GO

-- Extended Property Audit Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ExtPropAudit' AND schema_id = SCHEMA_ID('acadsync'))
BEGIN
    CREATE TABLE acadsync.ExtPropAudit (
        AuditId BIGINT IDENTITY(1,1) PRIMARY KEY,
        Timestamp DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        RuleId NVARCHAR(100) NOT NULL,
        EntityType NVARCHAR(50) NOT NULL,           -- Student, Document, etc.
        EntityId BIGINT NOT NULL,
        PropertyCode NVARCHAR(100) NOT NULL,
        OldValue NVARCHAR(4000) NULL,
        NewValue NVARCHAR(4000) NULL,
        Action NVARCHAR(50) NOT NULL,               -- repair:upsert, repair:normalize, etc.
        Severity NVARCHAR(20) NOT NULL,             -- info, warning, error, block
        Operator NVARCHAR(200) NOT NULL,            -- staff:123 or system
        Notes NVARCHAR(MAX) NULL,
        
        -- Indexes for common queries
        INDEX IX_ExtPropAudit_Timestamp (Timestamp DESC),
        INDEX IX_ExtPropAudit_Entity (EntityType, EntityId),
        INDEX IX_ExtPropAudit_Rule (RuleId),
        INDEX IX_ExtPropAudit_Property (PropertyCode)
    );
    PRINT 'Created ExtPropAudit table.';
END
ELSE
BEGIN
    PRINT 'ExtPropAudit table already exists.';
END
GO

-- Validation Runs Table - Track validation sessions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ValidationRuns' AND schema_id = SCHEMA_ID('acadsync'))
BEGIN
    CREATE TABLE acadsync.ValidationRuns (
        RunId BIGINT IDENTITY(1,1) PRIMARY KEY,
        StartTime DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        EndTime DATETIMEOFFSET NULL,
        Mode NVARCHAR(20) NOT NULL,                 -- validate, simulate, repair
        StaffId INT NOT NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Running', -- Running, Completed, Failed
        ViolationCount INT NULL,
        RepairedCount INT NULL,
        Notes NVARCHAR(MAX) NULL,
        
        INDEX IX_ValidationRuns_StartTime (StartTime DESC),
        INDEX IX_ValidationRuns_Status (Status),
        INDEX IX_ValidationRuns_Mode (Mode)
    );
    PRINT 'Created ValidationRuns table.';
END
ELSE
BEGIN
    PRINT 'ValidationRuns table already exists.';
END
GO

-- System Log Table - Application logging
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemLog' AND schema_id = SCHEMA_ID('acadsync'))
BEGIN
    CREATE TABLE acadsync.SystemLog (
        LogId BIGINT IDENTITY(1,1) PRIMARY KEY,
        Timestamp DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Level NVARCHAR(20) NOT NULL,                -- Debug, Info, Warning, Error, Critical
        Message NVARCHAR(MAX) NOT NULL,
        Exception NVARCHAR(MAX) NULL,
        Source NVARCHAR(100) NULL,
        
        INDEX IX_SystemLog_Timestamp (Timestamp DESC),
        INDEX IX_SystemLog_Level (Level)
    );
    PRINT 'Created SystemLog table.';
END
ELSE
BEGIN
    PRINT 'SystemLog table already exists.';
END
GO

-- Views for easier reporting
IF NOT EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ExtPropAuditSummary' AND schema_id = SCHEMA_ID('acadsync'))
BEGIN
    EXEC('CREATE VIEW acadsync.vw_ExtPropAuditSummary AS
    SELECT 
        CAST(Timestamp AS DATE) as AuditDate,
        RuleId,
        EntityType,
        PropertyCode,
        Action,
        Severity,
        COUNT(*) as ViolationCount,
        COUNT(CASE WHEN Action LIKE ''repair:%'' THEN 1 END) as RepairCount
    FROM acadsync.ExtPropAudit
    GROUP BY 
        CAST(Timestamp AS DATE),
        RuleId,
        EntityType,
        PropertyCode,
        Action,
        Severity');
    PRINT 'Created vw_ExtPropAuditSummary view.';
END
ELSE
BEGIN
    PRINT 'vw_ExtPropAuditSummary view already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ValidationRunSummary' AND schema_id = SCHEMA_ID('acadsync'))
BEGIN
    EXEC('CREATE VIEW acadsync.vw_ValidationRunSummary AS
    SELECT 
        RunId,
        StartTime,
        EndTime,
        Mode,
        Status,
        ViolationCount,
        RepairedCount,
        DATEDIFF(SECOND, StartTime, COALESCE(EndTime, SYSDATETIMEOFFSET())) as DurationSeconds,
        CASE 
            WHEN EndTime IS NULL THEN ''In Progress''
            WHEN Status = ''Completed'' THEN ''Success''
            ELSE ''Failed''
        END as Result
    FROM acadsync.ValidationRuns');
    PRINT 'Created vw_ValidationRunSummary view.';
END
ELSE
BEGIN
    PRINT 'vw_ValidationRunSummary view already exists.';
END
GO

-- Grant permissions (adjust as needed for your environment)
-- GRANT SELECT, INSERT, UPDATE ON acadsync.ExtPropAudit TO [YourAppUser];
-- GRANT SELECT, INSERT, UPDATE ON acadsync.ValidationRuns TO [YourAppUser];
-- GRANT SELECT, INSERT ON acadsync.SystemLog TO [YourAppUser];
-- GRANT SELECT ON acadsync.vw_ExtPropAuditSummary TO [YourAppUser];
-- GRANT SELECT ON acadsync.vw_ValidationRunSummary TO [YourAppUser];

PRINT 'AcadSync audit tables created successfully in acadsync schema.';
