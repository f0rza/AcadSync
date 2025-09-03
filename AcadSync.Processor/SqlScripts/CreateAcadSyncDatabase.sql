-- AcadSync Audit Database Creation Script
-- Run this script on your SQL Server instance to create the AcadSync audit database

-- Create the database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AcadSyncAudit')
BEGIN
    CREATE DATABASE AcadSyncAudit
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END
GO

USE AcadSyncAudit;
GO

-- Create the acadsync schema
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'acadsync')
BEGIN
    EXEC('CREATE SCHEMA acadsync');
END
GO

-- Grant permissions to the schema (adjust as needed for your environment)
-- GRANT CREATE TABLE, CREATE VIEW, CREATE PROCEDURE ON SCHEMA::acadsync TO [YourAppUser];
-- GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::acadsync TO [YourAppUser];

PRINT 'AcadSyncAudit database and acadsync schema created successfully.';
