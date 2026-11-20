-- ============================================================================
-- CLARK-WILSON INTEGRITY MODEL ROLLBACK SCRIPT
-- Secure Employee Payroll Management System (SEPS)
-- Platform: Microsoft SQL Server 2022
-- Database: SecureEmployeePayrollDB
-- Description: Safely removes the four additive Clark-Wilson tables in reverse
--              foreign-key dependency order without touching core business data.
-- ============================================================================

USE SecureEmployeePayrollDB;
GO

BEGIN TRANSACTION;

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CLARK_WILSON_APPROVAL]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[CLARK_WILSON_APPROVAL];
    PRINT 'Dropped table: CLARK_WILSON_APPROVAL';
END

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CLARK_WILSON_TRANSACTION]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[CLARK_WILSON_TRANSACTION];
    PRINT 'Dropped table: CLARK_WILSON_TRANSACTION';
END

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CLARK_WILSON_AUDIT]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[CLARK_WILSON_AUDIT];
    PRINT 'Dropped table: CLARK_WILSON_AUDIT';
END

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CLARK_WILSON_VERIFICATION_LOG]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[CLARK_WILSON_VERIFICATION_LOG];
    PRINT 'Dropped table: CLARK_WILSON_VERIFICATION_LOG';
END

COMMIT TRANSACTION;
PRINT 'Clark-Wilson Rollback Completed Successfully.';
GO
