using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

/// <summary>
/// Implementation of the Clark-Wilson Transformation Procedure Engine.
/// Guarantees that only authorized, well-formed transactions can modify Constrained Data Items (CDIs).
/// </summary>
public class TransformationProcedureService : ITransformationProcedureService
{
    private readonly PayrollDbContext _context;
    private readonly ISeparationOfDutiesService _sod;
    private readonly IIntegrityVerificationService _ivp;
    private readonly IIntegrityAssignmentService _biba;
    private readonly ILogger<TransformationProcedureService> _logger;

    public TransformationProcedureService(
        PayrollDbContext context,
        ISeparationOfDutiesService sod,
        IIntegrityVerificationService ivp,
        IIntegrityAssignmentService biba,
        ILogger<TransformationProcedureService> logger)
    {
        _context = context;
        _sod = sod;
        _ivp = ivp;
        _biba = biba;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Error, long TransactionId)> SubmitTransactionAsync(
        string transactionType,
        string targetEntityType,
        int targetEntityId,
        int submitterUserId,
        string? payloadJson = null,
        string? justification = null,
        string? ipAddress = null)
    {
        if (submitterUserId <= 0)
        {
            return (false, "Authenticated server-side submitter identity is required.", 0);
        }

        // Submitter must have at least SlightlyTrusted integrity to submit business proposals
        var submitterLevel = await _biba.GetUserIntegrityLevelAsync(submitterUserId);
        if (submitterLevel < IntegrityLevel.SlightlyTrusted)
        {
            await RecordAuditAsync(submitterUserId, "CLARK_WILSON_TP_DENIED", targetEntityType, targetEntityId, "DENIED",
                $"Untrusted user #{submitterUserId} cannot initiate Transformation Procedures.", ipAddress);
            return (false, "Biba Integrity Violation: Untrusted users cannot initiate Clark-Wilson Transformation Procedures.", 0);
        }

        try
        {
            var transaction = new ClarkWilsonTransaction
            {
                TransactionType = transactionType,
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId,
                PayloadJson = payloadJson,
                Status = "PENDING",
                SubmittedByUserId = submitterUserId,
                SubmittedAtUtc = DateTime.UtcNow,
                Justification = justification
            };

            _context.ClarkWilsonTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            await RecordAuditAsync(submitterUserId, "CLARK_WILSON_APPROVAL_REQUESTED", targetEntityType, targetEntityId, "SUCCESS",
                $"Transaction #{transaction.TransactionId} ({transactionType}) submitted for dual-control approval.", ipAddress);

            return (true, string.Empty, transaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit Clark-Wilson transaction {Type} for Entity {Entity} #{Id}", transactionType, targetEntityType, targetEntityId);
            return (false, ex.Message, 0);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Error)> ApproveTransactionAsync(
        long transactionId,
        int approverUserId,
        string? remarks = null,
        string? ipAddress = null)
    {
        if (approverUserId <= 0)
        {
            return (false, "Approver identity could not be verified from server session.");
        }

        var tx = await _context.ClarkWilsonTransactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId);
        if (tx == null)
        {
            return (false, $"Transaction #{transactionId} not found.");
        }

        // 1. Enforce Separation of Duties (Dual-Control)
        var (sodAllowed, sodReason) = await _sod.ValidateDualControlAsync(transactionId, approverUserId);
        if (!sodAllowed)
        {
            await RecordAuditAsync(approverUserId, "CLARK_WILSON_SOD_VIOLATION", tx.TargetEntityType, tx.TargetEntityId, "DENIED",
                sodReason, ipAddress);
            return (false, sodReason);
        }

        // 2. Enforce Biba write check for the approver
        var (bibaAllowed, bibaReason, _, _) = await _biba.CheckWriteAccessAsync(approverUserId, tx.TargetEntityType, tx.TargetEntityId, ipAddress);
        if (!bibaAllowed)
        {
            await RecordAuditAsync(approverUserId, "CLARK_WILSON_TP_DENIED", tx.TargetEntityType, tx.TargetEntityId, "DENIED",
                $"Biba policy blocked approver: {bibaReason}", ipAddress);
            return (false, $"Biba Integrity Violation: {bibaReason}");
        }

        // 3. Execute appropriate IVP before committing
        bool ivpPassed = true;
        string ivpErrors = string.Empty;

        switch (tx.TargetEntityType.ToLowerInvariant())
        {
            case "payroll":
                var payroll = await _context.Payrolls.FirstOrDefaultAsync(p => p.PayrollId == tx.TargetEntityId);
                if (payroll != null)
                {
                    var (pValid, pErr) = await _ivp.VerifyPayrollRunAsync(payroll);
                    ivpPassed = pValid;
                    ivpErrors = string.Join("; ", pErr);
                }
                break;

            case "salarystructure":
                var salary = await _context.SalaryStructures.FirstOrDefaultAsync(s => s.SalaryStructureId == tx.TargetEntityId);
                if (salary != null)
                {
                    var (sValid, sErr) = await _ivp.VerifySalaryStructureAsync(salary);
                    ivpPassed = sValid;
                    ivpErrors = string.Join("; ", sErr);
                }
                break;

            case "bankaccount":
                var bank = await _context.BankAccounts.FirstOrDefaultAsync(b => b.BankAccountId == tx.TargetEntityId);
                if (bank != null)
                {
                    var (bValid, bErr) = await _ivp.VerifyBankAccountAsync(bank);
                    ivpPassed = bValid;
                    ivpErrors = string.Join("; ", bErr);
                }
                break;

            case "employee":
                var emp = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == tx.TargetEntityId);
                if (emp != null)
                {
                    var (eValid, eErr) = await _ivp.VerifyEmployeeAsync(emp);
                    ivpPassed = eValid;
                    ivpErrors = string.Join("; ", eErr);
                }
                break;
        }

        if (!ivpPassed)
        {
            await RecordAuditAsync(approverUserId, "CLARK_WILSON_IVP_FAILED", tx.TargetEntityType, tx.TargetEntityId, "FAILED",
                $"Integrity Verification Procedure failed prior to approval: {ivpErrors}", ipAddress);
            return (false, $"Clark-Wilson Integrity Verification Failed: {ivpErrors}");
        }

        // 4. Atomic transaction commit
        await using var dbTx = await _context.Database.BeginTransactionAsync();
        try
        {
            tx.Status = "APPROVED";

            var approval = new ClarkWilsonApproval
            {
                TransactionId = transactionId,
                ApproverUserId = approverUserId,
                Decision = "APPROVED",
                DecisionAtUtc = DateTime.UtcNow,
                Remarks = remarks
            };
            _context.ClarkWilsonApprovals.Add(approval);

            await _context.SaveChangesAsync();
            await dbTx.CommitAsync();

            await RecordAuditAsync(approverUserId, "CLARK_WILSON_APPROVED", tx.TargetEntityType, tx.TargetEntityId, "SUCCESS",
                $"Transaction #{transactionId} approved by independent user #{approverUserId}. Remarks: {remarks}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Transaction rollback during Clark-Wilson approval for Tx #{TxId}", transactionId);
            return (false, $"Database transaction failed and was rolled back: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Error)> RejectTransactionAsync(
        long transactionId,
        int approverUserId,
        string reason,
        string? ipAddress = null)
    {
        if (approverUserId <= 0)
        {
            return (false, "Approver identity could not be verified from server session.");
        }

        var tx = await _context.ClarkWilsonTransactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId);
        if (tx == null)
        {
            return (false, $"Transaction #{transactionId} not found.");
        }

        if (!string.Equals(tx.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Transaction #{transactionId} is already in state '{tx.Status}'. Only PENDING transactions can be rejected.");
        }

        // Approver cannot be submitter even for formal rejection
        var (sodAllowed, sodReason) = await _sod.ValidateDualControlAsync(transactionId, approverUserId);
        if (!sodAllowed)
        {
            await RecordAuditAsync(approverUserId, "CLARK_WILSON_SOD_VIOLATION", tx.TargetEntityType, tx.TargetEntityId, "DENIED",
                sodReason, ipAddress);
            return (false, sodReason);
        }

        await using var dbTx = await _context.Database.BeginTransactionAsync();
        try
        {
            tx.Status = "REJECTED";

            var approval = new ClarkWilsonApproval
            {
                TransactionId = transactionId,
                ApproverUserId = approverUserId,
                Decision = "REJECTED",
                DecisionAtUtc = DateTime.UtcNow,
                Remarks = reason
            };
            _context.ClarkWilsonApprovals.Add(approval);

            await _context.SaveChangesAsync();
            await dbTx.CommitAsync();

            await RecordAuditAsync(approverUserId, "CLARK_WILSON_REJECTED", tx.TargetEntityType, tx.TargetEntityId, "SUCCESS",
                $"Transaction #{transactionId} rejected by user #{approverUserId}. Reason: {reason}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to reject Clark-Wilson transaction #{TxId}", transactionId);
            return (false, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Error, T? Result)> ExecuteAtomicTPAsync<T>(
        string tpName,
        string targetEntityType,
        int targetEntityId,
        int currentUserId,
        Func<Task<(bool Success, string Error, T? Result)>> action,
        string? ipAddress = null)
    {
        await RecordAuditAsync(currentUserId, "CLARK_WILSON_TP_STARTED", targetEntityType, targetEntityId, "SUCCESS",
            $"Initiating Transformation Procedure: {tpName}", ipAddress);

        // 1. Enforce Biba write check if target entity is identified
        if (targetEntityId > 0 && currentUserId > 0)
        {
            var (bibaAllowed, bibaReason, _, _) = await _biba.CheckWriteAccessAsync(currentUserId, targetEntityType, targetEntityId, ipAddress);
            if (!bibaAllowed)
            {
                await RecordAuditAsync(currentUserId, "CLARK_WILSON_TP_DENIED", targetEntityType, targetEntityId, "DENIED",
                    $"Biba No Write Up violation in TP {tpName}: {bibaReason}", ipAddress);
                return (false, $"Biba Integrity Violation: {bibaReason}", default);
            }
        }

        // 2. Execute procedure within an isolated transaction
        await using var dbTx = await _context.Database.BeginTransactionAsync();
        try
        {
            var (success, error, result) = await action();
            if (!success)
            {
                await dbTx.RollbackAsync();
                await RecordAuditAsync(currentUserId, "CLARK_WILSON_TP_DENIED", targetEntityType, targetEntityId, "DENIED",
                    $"TP {tpName} failed business validation: {error}", ipAddress);
                return (false, error, default);
            }

            await dbTx.CommitAsync();

            await RecordAuditAsync(currentUserId, "CLARK_WILSON_TP_ALLOWED", targetEntityType, targetEntityId, "SUCCESS",
                $"Transformation Procedure {tpName} completed and committed successfully.", ipAddress);

            return (true, string.Empty, result);
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Exception encountered during TP {TPName} execution", tpName);

            await RecordAuditAsync(currentUserId, "CLARK_WILSON_TP_DENIED", targetEntityType, targetEntityId, "FAILED",
                $"TP {tpName} rolled back due to error: {ex.Message}", ipAddress);

            return (false, $"Transaction failed and was safely rolled back: {ex.Message}", default);
        }
    }

    /// <inheritdoc />
    public async Task RecordAuditAsync(
        int userId,
        string eventType,
        string targetEntity,
        int? targetEntityId,
        string outcome,
        string? reason = null,
        string? ipAddress = null)
    {
        try
        {
            var audit = new ClarkWilsonAudit
            {
                UserId = userId > 0 ? userId : 1, // Fallback to System/Admin ID 1 if unauthenticated attempt
                EventType = eventType,
                TargetEntity = targetEntity,
                TargetEntityId = targetEntityId,
                Outcome = outcome,
                Reason = reason,
                IpAddress = ipAddress,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.ClarkWilsonAudits.Add(audit);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write Clark-Wilson security audit event {Event} for User #{UserId}", eventType, userId);
        }
    }
}
