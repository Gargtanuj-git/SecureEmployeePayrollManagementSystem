using System.Threading.Tasks;

namespace SecureEmployeePayrollSystem.Services;

/// <summary>
/// Clark-Wilson Separation of Duties (SoD) Service.
/// Enforces dual-control / two-man approval rules preventing self-approval on high-impact financial transactions.
/// </summary>
public interface ISeparationOfDutiesService
{
    /// <summary>
    /// Verifies that an approver is distinct from the transaction submitter (Submitter != Approver)
    /// and that the transaction is eligible for approval.
    /// </summary>
    Task<(bool IsAllowed, string Reason)> ValidateDualControlAsync(long transactionId, int approverUserId);

    /// <summary>
    /// Verifies dual control directly between a submitter and an approver ID for a specific transaction type.
    /// </summary>
    bool ValidateSubmitterNotApprover(int submitterUserId, int approverUserId, string transactionType, out string reason);
}
