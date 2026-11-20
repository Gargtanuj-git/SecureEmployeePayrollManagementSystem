-- ============================================================================
-- CLARK-WILSON INTEGRITY MODEL MIGRATION SCRIPT
-- Secure Employee Payroll Management System (SEPS)
-- Platform: Microsoft SQL Server 2022
-- Database: SecureEmployeePayrollDB
-- Description: Creates additive tables for Transformation Procedures (TPs),
--              Separation of Duties (SoD), Integrity Verification Procedures (IVPs),
--              and dedicated tamper-evident Clark-Wilson audit logs.
-- ============================================================================

USE SecureEmployeePayrollDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

-- 1. CLARK_WILSON_TRANSACTION
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CLARK_WILSON_TRANSACTION]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CLARK_WILSON_TRANSACTION] (
        [Transaction_ID] BIGINT IDENTITY(1001,1) NOT NULL,
        [Transaction_Type] NVARCHAR(50) NOT NULL,
        [Target_Entity_Type] NVARCHAR(50) NOT NULL,
        [Target_Entity_ID] INT NOT NULL,
        [Payload_Json] NVARCHAR(MAX) NULL,
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'PENDING',
        [Submitted_By_User_ID] INT NOT NULL,
        [Submitted_At_Utc] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [Justification] NVARCHAR(500) NULL,
        [Concurrency_Token] ROWVERSION NOT NULL,
        CONSTRAINT [PK_CLARK_WILSON_TRANSACTION] PRIMARY KEY CLUSTERED ([Transaction_ID] ASC),
        CONSTRAINT [FK_CW_TX_SUBMITTED_BY] FOREIGN KEY ([Submitted_By_User_ID]) REFERENCES [dbo].[USER_ACCOUNT] ([User_ID]),
        CONSTRAINT [CK_CW_TX_STATUS] CHECK ([Status] IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED', 'EXPIRED'))
    );

    CREATE NONCLUSTERED INDEX [IX_CW_TX_Status_Type] ON [dbo].[CLARK_WILSON_TRANSACTION] ([Status], [Transaction_Type]);
    CREATE NONCLUSTERED INDEX [IX_CW_TX_Target] ON [dbo].[CLARK_WILSON_TRANSACTION] ([Target_Entity_Type], [Target_Entity_ID]);
    CREATE NONCLUSTERED INDEX [IX_CW_TX_SubmittedBy] ON [dbo].[CLARK_WILSON_TRANSACTION] ([Submitted_By_User_ID], [Submitted_At_Utc]);
    PRINT 'Created table: CLARK_WILSON_TRANSACTION';
END
ELSE
BEGIN
    PRINT 'Table CLARK_WILSON_TRANSACTION already exists.';
END
GO

-- 2. CLARK_WILSON_APPROVAL
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CLARK_WILSON_APPROVAL]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CLARK_WILSON_APPROVAL] (
        [Approval_ID] BIGINT IDENTITY(1,1) NOT NULL,
        [Transaction_ID] BIGINT NOT NULL,
        [Approver_User_ID] INT NOT NULL,
        [Decision] NVARCHAR(20) NOT NULL,
        [Decision_At_Utc] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [Remarks] NVARCHAR(500) NULL,
        CONSTRAINT [PK_CLARK_WILSON_APPROVAL] PRIMARY KEY CLUSTERED ([Approval_ID] ASC),
        CONSTRAINT [FK_CW_APPROVAL_TX] FOREIGN KEY ([Transaction_ID]) REFERENCES [dbo].[CLARK_WILSON_TRANSACTION] ([Transaction_ID]),
        CONSTRAINT [FK_CW_APPROVAL_USER] FOREIGN KEY ([Approver_User_ID]) REFERENCES [dbo].[USER_ACCOUNT] ([User_ID]),
        CONSTRAINT [CK_CW_APPROVAL_DECISION] CHECK ([Decision] IN ('APPROVED', 'REJECTED'))
    );

    CREATE NONCLUSTERED INDEX [IX_CW_APPROVAL_TxId] ON [dbo].[CLARK_WILSON_APPROVAL] ([Transaction_ID]);
    CREATE NONCLUSTERED INDEX [IX_CW_APPROVAL_Approver] ON [dbo].[CLARK_WILSON_APPROVAL] ([Approver_User_ID]);
    PRINT 'Created table: CLARK_WILSON_APPROVAL';
END
ELSE
BEGIN
    PRINT 'Table CLARK_WILSON_APPROVAL already exists.';
END
GO

-- 3. CLARK_WILSON_AUDIT
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CLARK_WILSON_AUDIT]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CLARK_WILSON_AUDIT] (
        [Audit_ID] BIGINT IDENTITY(1,1) NOT NULL,
        [User_ID] INT NOT NULL,
        [Event_Type] NVARCHAR(60) NOT NULL,
        [Target_Entity] NVARCHAR(50) NOT NULL,
        [Target_Entity_ID] INT NULL,
        [Outcome] NVARCHAR(20) NOT NULL,
        [Reason] NVARCHAR(500) NULL,
        [Created_At_Utc] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [Ip_Address] NVARCHAR(50) NULL,
        CONSTRAINT [PK_CLARK_WILSON_AUDIT] PRIMARY KEY CLUSTERED ([Audit_ID] ASC),
        CONSTRAINT [FK_CW_AUDIT_USER] FOREIGN KEY ([User_ID]) REFERENCES [dbo].[USER_ACCOUNT] ([User_ID]),
        CONSTRAINT [CK_CW_AUDIT_OUTCOME] CHECK ([Outcome] IN ('SUCCESS', 'DENIED', 'FAILED'))
    );

    CREATE NONCLUSTERED INDEX [IX_CW_AUDIT_User_Date] ON [dbo].[CLARK_WILSON_AUDIT] ([User_ID], [Created_At_Utc]);
    CREATE NONCLUSTERED INDEX [IX_CW_AUDIT_Event] ON [dbo].[CLARK_WILSON_AUDIT] ([Event_Type], [Created_At_Utc]);
    CREATE NONCLUSTERED INDEX [IX_CW_AUDIT_Target] ON [dbo].[CLARK_WILSON_AUDIT] ([Target_Entity], [Target_Entity_ID]);
    PRINT 'Created table: CLARK_WILSON_AUDIT';
END
ELSE
BEGIN
    PRINT 'Table CLARK_WILSON_AUDIT already exists.';
END
GO

-- 4. CLARK_WILSON_VERIFICATION_LOG
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CLARK_WILSON_VERIFICATION_LOG]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CLARK_WILSON_VERIFICATION_LOG] (
        [Log_ID] BIGINT IDENTITY(1,1) NOT NULL,
        [IVP_Name] NVARCHAR(60) NOT NULL,
        [Target_Entity] NVARCHAR(50) NOT NULL,
        [Target_Entity_ID] INT NOT NULL,
        [Is_Consistent] BIT NOT NULL,
        [Validation_Errors] NVARCHAR(MAX) NULL,
        [Verified_At_Utc] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_CLARK_WILSON_VERIFICATION_LOG] PRIMARY KEY CLUSTERED ([Log_ID] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_CW_VERIF_Entity] ON [dbo].[CLARK_WILSON_VERIFICATION_LOG] ([Target_Entity], [Target_Entity_ID]);
    CREATE NONCLUSTERED INDEX [IX_CW_VERIF_Consistent] ON [dbo].[CLARK_WILSON_VERIFICATION_LOG] ([Is_Consistent], [Verified_At_Utc]);
    PRINT 'Created table: CLARK_WILSON_VERIFICATION_LOG';
END
ELSE
BEGIN
    PRINT 'Table CLARK_WILSON_VERIFICATION_LOG already exists.';
END
GO

COMMIT TRANSACTION;
PRINT 'Clark-Wilson Migration Completed Successfully.';
GO
