-- ============================================================================
-- SEPS BIBA INTEGRITY MODEL DATABASE MIGRATION SCRIPT
-- Target Database: SecureEmployeePayrollDB
-- Platform: Microsoft SQL Server 2022
-- ============================================================================

USE SecureEmployeePayrollDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Table: USER_INTEGRITY_CLASSIFICATION
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'USER_INTEGRITY_CLASSIFICATION')
BEGIN
    CREATE TABLE dbo.USER_INTEGRITY_CLASSIFICATION (
        User_Integrity_ID INT IDENTITY(1,1) NOT NULL,
        User_ID INT NOT NULL,
        Integrity_Level INT NOT NULL,
        Assigned_By INT NOT NULL,
        Assigned_At_Utc DATETIME2(7) NOT NULL CONSTRAINT DF_USER_INTEGRITY_ASSIGNED_AT DEFAULT (SYSUTCDATETIME()),
        Reason NVARCHAR(250) NULL,
        Is_Active BIT NOT NULL CONSTRAINT DF_USER_INTEGRITY_IS_ACTIVE DEFAULT (1),
        CONSTRAINT PK_USER_INTEGRITY_CLASSIFICATION PRIMARY KEY CLUSTERED (User_Integrity_ID ASC),
        CONSTRAINT CK_USER_INTEGRITY_LEVEL CHECK (Integrity_Level BETWEEN 1 AND 4),
        CONSTRAINT FK_USER_INTEGRITY_USER FOREIGN KEY (User_ID) REFERENCES dbo.USER_ACCOUNT (User_ID) ON DELETE CASCADE,
        CONSTRAINT FK_USER_INTEGRITY_ASSIGNED_BY FOREIGN KEY (Assigned_By) REFERENCES dbo.USER_ACCOUNT (User_ID)
    );

    CREATE NONCLUSTERED INDEX IX_USER_INTEGRITY_USER_ACTIVE 
    ON dbo.USER_INTEGRITY_CLASSIFICATION (User_ID, Is_Active) 
    INCLUDE (Integrity_Level, Assigned_At_Utc);

    PRINT 'Created table USER_INTEGRITY_CLASSIFICATION.';
END
GO

-- 2. Table: OBJECT_INTEGRITY_CLASSIFICATION
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OBJECT_INTEGRITY_CLASSIFICATION')
BEGIN
    CREATE TABLE dbo.OBJECT_INTEGRITY_CLASSIFICATION (
        Object_Integrity_ID INT IDENTITY(1,1) NOT NULL,
        Entity_Type NVARCHAR(50) NOT NULL,
        Entity_ID INT NOT NULL,
        Integrity_Level INT NOT NULL,
        Assigned_By INT NOT NULL,
        Assigned_At_Utc DATETIME2(7) NOT NULL CONSTRAINT DF_OBJECT_INTEGRITY_ASSIGNED_AT DEFAULT (SYSUTCDATETIME()),
        Reason NVARCHAR(250) NULL,
        Is_Active BIT NOT NULL CONSTRAINT DF_OBJECT_INTEGRITY_IS_ACTIVE DEFAULT (1),
        CONSTRAINT PK_OBJECT_INTEGRITY_CLASSIFICATION PRIMARY KEY CLUSTERED (Object_Integrity_ID ASC),
        CONSTRAINT CK_OBJECT_INTEGRITY_LEVEL CHECK (Integrity_Level BETWEEN 1 AND 4),
        CONSTRAINT FK_OBJECT_INTEGRITY_ASSIGNED_BY FOREIGN KEY (Assigned_By) REFERENCES dbo.USER_ACCOUNT (User_ID)
    );

    CREATE NONCLUSTERED INDEX IX_OBJECT_INTEGRITY_ENTITY_ACTIVE 
    ON dbo.OBJECT_INTEGRITY_CLASSIFICATION (Entity_Type, Entity_ID, Is_Active) 
    INCLUDE (Integrity_Level, Assigned_At_Utc);

    PRINT 'Created table OBJECT_INTEGRITY_CLASSIFICATION.';
END
GO

-- 3. Table: INTEGRITY_CLASSIFICATION_HISTORY
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INTEGRITY_CLASSIFICATION_HISTORY')
BEGIN
    CREATE TABLE dbo.INTEGRITY_CLASSIFICATION_HISTORY (
        History_ID BIGINT IDENTITY(1,1) NOT NULL,
        Entity_Type NVARCHAR(50) NOT NULL,
        Entity_ID INT NOT NULL,
        Previous_Integrity_Level INT NOT NULL,
        New_Integrity_Level INT NOT NULL,
        Changed_By INT NOT NULL,
        Changed_At_Utc DATETIME2(7) NOT NULL CONSTRAINT DF_INTEGRITY_HISTORY_CHANGED_AT DEFAULT (SYSUTCDATETIME()),
        Reason NVARCHAR(500) NOT NULL,
        Approval_Reference NVARCHAR(100) NULL,
        CONSTRAINT PK_INTEGRITY_CLASSIFICATION_HISTORY PRIMARY KEY CLUSTERED (History_ID ASC),
        CONSTRAINT CK_INTEGRITY_HISTORY_PREV_LEVEL CHECK (Previous_Integrity_Level BETWEEN 1 AND 4),
        CONSTRAINT CK_INTEGRITY_HISTORY_NEW_LEVEL CHECK (New_Integrity_Level BETWEEN 1 AND 4),
        CONSTRAINT FK_INTEGRITY_HISTORY_CHANGED_BY FOREIGN KEY (Changed_By) REFERENCES dbo.USER_ACCOUNT (User_ID)
    );

    CREATE NONCLUSTERED INDEX IX_INTEGRITY_HISTORY_ENTITY 
    ON dbo.INTEGRITY_CLASSIFICATION_HISTORY (Entity_Type, Entity_ID, Changed_At_Utc);

    PRINT 'Created table INTEGRITY_CLASSIFICATION_HISTORY.';
END
GO

-- 4. Table: INTEGRITY_SECURITY_AUDIT
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INTEGRITY_SECURITY_AUDIT')
BEGIN
    CREATE TABLE dbo.INTEGRITY_SECURITY_AUDIT (
        Audit_ID BIGINT IDENTITY(1,1) NOT NULL,
        User_ID INT NOT NULL,
        Operation NVARCHAR(30) NOT NULL,
        Entity_Type NVARCHAR(50) NOT NULL,
        Entity_ID INT NOT NULL,
        Subject_Integrity_Level INT NOT NULL,
        Object_Integrity_Level INT NOT NULL,
        Decision NVARCHAR(20) NOT NULL,
        Reason NVARCHAR(500) NULL,
        IP_Address VARCHAR(45) NULL,
        Created_At_Utc DATETIME2(7) NOT NULL CONSTRAINT DF_INTEGRITY_AUDIT_CREATED_AT DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_INTEGRITY_SECURITY_AUDIT PRIMARY KEY CLUSTERED (Audit_ID ASC),
        CONSTRAINT CK_INTEGRITY_AUDIT_SUBJECT_LEVEL CHECK (Subject_Integrity_Level BETWEEN 1 AND 4),
        CONSTRAINT CK_INTEGRITY_AUDIT_OBJECT_LEVEL CHECK (Object_Integrity_Level BETWEEN 1 AND 4),
        CONSTRAINT FK_INTEGRITY_AUDIT_USER FOREIGN KEY (User_ID) REFERENCES dbo.USER_ACCOUNT (User_ID)
    );

    CREATE NONCLUSTERED INDEX IX_INTEGRITY_AUDIT_USER_DATE 
    ON dbo.INTEGRITY_SECURITY_AUDIT (User_ID, Created_At_Utc);

    CREATE NONCLUSTERED INDEX IX_INTEGRITY_AUDIT_ENTITY 
    ON dbo.INTEGRITY_SECURITY_AUDIT (Entity_Type, Entity_ID, Created_At_Utc);

    PRINT 'Created table INTEGRITY_SECURITY_AUDIT.';
END
GO

-- ============================================================================
-- 5. INITIAL DATA SEEDING (Idempotent)
-- ============================================================================

-- Seed User Integrity Classifications from Existing Roles
DECLARE @AdminId INT = (SELECT TOP 1 User_ID FROM dbo.USER_ACCOUNT WHERE Username = 'aarav.mehta');
IF @AdminId IS NULL SET @AdminId = 1;

-- Seed Admin as HighlyTrusted (4)
IF NOT EXISTS (SELECT 1 FROM dbo.USER_INTEGRITY_CLASSIFICATION WHERE User_ID = @AdminId)
BEGIN
    INSERT INTO dbo.USER_INTEGRITY_CLASSIFICATION (User_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
    VALUES (@AdminId, 4, @AdminId, 'Initial Admin Security Baseline', 1);
END

-- Seed HR Manager (riya.sharma) as Trusted (3)
DECLARE @HrId INT = (SELECT TOP 1 User_ID FROM dbo.USER_ACCOUNT WHERE Username = 'riya.sharma');
IF @HrId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.USER_INTEGRITY_CLASSIFICATION WHERE User_ID = @HrId)
BEGIN
    INSERT INTO dbo.USER_INTEGRITY_CLASSIFICATION (User_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
    VALUES (@HrId, 3, @AdminId, 'Initial HR Manager Security Baseline', 1);
END

-- Seed Payroll Manager (vikram.singh) as Trusted (3)
DECLARE @PayrollMgrId INT = (SELECT TOP 1 User_ID FROM dbo.USER_ACCOUNT WHERE Username = 'vikram.singh');
IF @PayrollMgrId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.USER_INTEGRITY_CLASSIFICATION WHERE User_ID = @PayrollMgrId)
BEGIN
    INSERT INTO dbo.USER_INTEGRITY_CLASSIFICATION (User_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
    VALUES (@PayrollMgrId, 3, @AdminId, 'Initial Payroll Manager Security Baseline', 1);
END

-- Seed all remaining active users as SlightlyTrusted (2)
INSERT INTO dbo.USER_INTEGRITY_CLASSIFICATION (User_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
SELECT u.User_ID, 2, @AdminId, 'Initial Employee Role Baseline', 1
FROM dbo.USER_ACCOUNT u
WHERE u.User_ID NOT IN (SELECT User_ID FROM dbo.USER_INTEGRITY_CLASSIFICATION)
  AND u.Account_Status = 'Active';

-- Seed Object Integrity Classifications:
-- 1. Active Salary Structures -> HighlyTrusted (4)
INSERT INTO dbo.OBJECT_INTEGRITY_CLASSIFICATION (Entity_Type, Entity_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
SELECT 'SalaryStructure', s.Salary_Structure_ID, 4, @AdminId, 'Approved Corporate Salary Structure', 1
FROM dbo.SALARY_STRUCTURE s
WHERE s.Status = 'Active'
  AND NOT EXISTS (SELECT 1 FROM dbo.OBJECT_INTEGRITY_CLASSIFICATION WHERE Entity_Type = 'SalaryStructure' AND Entity_ID = s.Salary_Structure_ID);

-- 2. Finalized / Paid Payroll records -> HighlyTrusted (4)
INSERT INTO dbo.OBJECT_INTEGRITY_CLASSIFICATION (Entity_Type, Entity_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
SELECT 'Payroll', p.Payroll_ID, 4, @AdminId, 'Finalized & Paid Payroll Record', 1
FROM dbo.PAYROLL p
WHERE p.Payroll_Status IN ('Paid', 'Approved')
  AND NOT EXISTS (SELECT 1 FROM dbo.OBJECT_INTEGRITY_CLASSIFICATION WHERE Entity_Type = 'Payroll' AND Entity_ID = p.Payroll_ID);

-- 3. Primary Verified Bank Accounts -> HighlyTrusted (4)
INSERT INTO dbo.OBJECT_INTEGRITY_CLASSIFICATION (Entity_Type, Entity_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
SELECT 'BankAccount', b.Bank_Account_ID, 4, @AdminId, 'Verified Primary Bank Account', 1
FROM dbo.BANK_ACCOUNT b
WHERE b.Is_Primary = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.OBJECT_INTEGRITY_CLASSIFICATION WHERE Entity_Type = 'BankAccount' AND Entity_ID = b.Bank_Account_ID);

-- 4. Active Verified Employees -> Trusted (3)
INSERT INTO dbo.OBJECT_INTEGRITY_CLASSIFICATION (Entity_Type, Entity_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
SELECT 'Employee', e.Employee_ID, 3, @AdminId, 'Verified Active Employee Profile', 1
FROM dbo.EMPLOYEE e
WHERE e.Employment_Status = 'Active'
  AND NOT EXISTS (SELECT 1 FROM dbo.OBJECT_INTEGRITY_CLASSIFICATION WHERE Entity_Type = 'Employee' AND Entity_ID = e.Employee_ID);

-- 5. Active Verified Departments -> Trusted (3)
INSERT INTO dbo.OBJECT_INTEGRITY_CLASSIFICATION (Entity_Type, Entity_ID, Integrity_Level, Assigned_By, Reason, Is_Active)
SELECT 'Department', d.Department_ID, 3, @AdminId, 'Verified Department Structure', 1
FROM dbo.DEPARTMENT d
WHERE d.Department_Status = 'Active'
  AND NOT EXISTS (SELECT 1 FROM dbo.OBJECT_INTEGRITY_CLASSIFICATION WHERE Entity_Type = 'Department' AND Entity_ID = d.Department_ID);

PRINT 'SEPS Biba Integrity Model Database Migration Completed Successfully.';
GO
