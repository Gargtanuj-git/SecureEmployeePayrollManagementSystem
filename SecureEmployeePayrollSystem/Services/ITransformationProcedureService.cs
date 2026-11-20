using System;
using System.Threading.Tasks;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

/// <summary>
/// Clark-Wilson Transformation Procedure (TP) Engine.
/// Orchestrates well-formed, authorized transactions, enforces IVPs and SoD,
/// and guarantees atomic database transactions and audit logging.
/// </summary>
public interface ITransformationProcedureService
{
    /// <summary>
    /// Registers a pending transaction requiring dual-control approval (e.g. Payroll approval, Salary revision, Bank update).
    /// </summary>
    Task<(bool Success, string Error, long TransactionId)> SubmitTransactionAsync(
        string transactionType,
        string targetEntityType,
        int targetEntityId,
        int submitterUserId,
        string? payloadJson = null,
        string? justification = null,
        string? ipAddress = null);

    /// <summary>
    /// Evaluates and completes an approval TP with Separation of Duties and IVP checks.
    /// </summary>
    Task<(bool Success, string Error)> ApproveTransactionAsync(
        long transactionId,
        int approverUserId,
        string? remarks = null,
        string? ipAddress = null);

    /// <summary>
    /// Rejects a pending Clark-Wilson transaction.
    /// </summary>
    Task<(bool Success, string Error)> RejectTransactionAsync(
        long transactionId,
        int approverUserId,
        string reason,
        string? ipAddress = null);

    /// <summary>
    /// Executes a direct transformation procedure within an atomic transaction, validating Biba policy and logging Clark-Wilson audit events.
    /// </summary>
    Task<(bool Success, string Error, T? Result)> ExecuteAtomicTPAsync<T>(
        string tpName,
        string targetEntityType,
        int targetEntityId,
        int currentUserId,
        Func<Task<(bool Success, string Error, T? Result)>> action,
        string? ipAddress = null);

    /// <summary>
    /// Records a formal event in the Clark-Wilson security audit log.
    /// </summary>
    Task RecordAuditAsync(
        int userId,
        string eventType,
        string targetEntity,
        int? targetEntityId,
        string outcome,
        string? reason = null,
        string? ipAddress = null);
}
