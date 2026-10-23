SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
-- Apply current query indexes without EF migration history or data reset.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Applications') AND name=N'IX_Applications_ApplicantId_CreatedAtUtc')
CREATE INDEX [IX_Applications_ApplicantId_CreatedAtUtc] ON [Applications] ([ApplicantId], [CreatedAtUtc] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Applications') AND name=N'IX_Applications_CreatedAtUtc')
CREATE INDEX [IX_Applications_CreatedAtUtc] ON [Applications] ([CreatedAtUtc] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Applications') AND name=N'IX_Applications_StatusId_CreatedAtUtc')
CREATE INDEX [IX_Applications_StatusId_CreatedAtUtc] ON [Applications] ([StatusId], [CreatedAtUtc] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Applications') AND name=N'IX_Applications_UnitId_StatusId_CreatedAtUtc')
CREATE INDEX [IX_Applications_UnitId_StatusId_CreatedAtUtc] ON [Applications] ([UnitId], [StatusId], [CreatedAtUtc] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Properties') AND name=N'IX_Properties_Name')
CREATE INDEX [IX_Properties_Name] ON [Properties] ([Name]);
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Applications') AND name=N'IX_Applications_ApplicantId')
DROP INDEX [IX_Applications_ApplicantId] ON [dbo].[Applications];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Applications') AND name=N'IX_Applications_StatusId')
DROP INDEX [IX_Applications_StatusId] ON [dbo].[Applications];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Applications') AND name=N'IX_Applications_UnitId')
DROP INDEX [IX_Applications_UnitId] ON [dbo].[Applications];
COMMIT TRANSACTION;
