using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureEmployeePayrollSystem.Data;

namespace SecureEmployeePayrollSystem.Services;

/// <summary>
/// Implementation of Clark-Wilson Separation of Duties (SoD).
/// </summary>
public class SeparationOfDutiesService : ISeparationOfDutiesService
{
    private readonly PayrollDbContext _context;
    private readonly ILogger<SeparationOfDutiesService> _logger;

    public SeparationOfDutiesService(
        PayrollDbContext context,
        ILogger<SeparationOfDutiesService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool IsAllowed, string Reason)> ValidateDualControlAsync(long transactionId, int approverUserId)
    {
        if (transactionId <= 0)
        {
            return (false, "Invalid transaction identifier.");
        }

        if (approverUserId <= 0)
        {
            return (false, "Approver identity could not be verified from server-side session.");
        }

        var transaction = await _context.ClarkWilsonTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

        if (transaction == null)
        {
            return (false, $"Clark-Wilson Transaction #{transactionId} was not found.");
        }

        if (string.Equals(transaction.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Transaction #{transactionId} has already been approved. Duplicate approval is rejected.");
        }

        if (string.Equals(transaction.Status, "REJECTED", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Transaction #{transactionId} was previously rejected and cannot be re-approved.");
        }

        if (!string.Equals(transaction.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Transaction #{transactionId} is not in a PENDING state (Current state: {transaction.Status}).");
        }

        // Rule 1: The submitter cannot approve their own transaction
        if (transaction.SubmittedByUserId == approverUserId)
        {
            _logger.LogWarning("SoD Violation: User #{UserId} attempted self-approval on Transaction #{TxId} ({TxType})",
                approverUserId, transactionId, transaction.TransactionType);
            return (false, $"Clark-Wilson Separation of Duties Violation: User #{approverUserId} submitted this transaction and cannot approve their own operation.");
        }

        return (true, string.Empty);
    }

    /// <inheritdoc />
    public bool ValidateSubmitterNotApprover(int submitterUserId, int approverUserId, string transactionType, out string reason)
    {
        if (submitterUserId > 0 && submitterUserId == approverUserId)
        {
            reason = $"Clark-Wilson Separation of Duties Violation: Submitter #{submitterUserId} cannot approve their own {transactionType} transaction.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
