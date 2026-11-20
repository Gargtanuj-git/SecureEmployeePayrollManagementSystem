using System.Collections.Generic;
using System.Threading.Tasks;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

/// <summary>
/// Clark-Wilson Integrity Verification Procedures (IVPs) Suite.
/// Validates that Constrained Data Items (CDIs) conform to established consistency rules.
/// </summary>
public interface IIntegrityVerificationService
{
    /// <summary>
    /// Executes IVP on an Employee CDI.
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> VerifyEmployeeAsync(Employee employee);

    /// <summary>
    /// Executes IVP on a Salary Structure and its Components.
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> VerifySalaryStructureAsync(SalaryStructure structure, IEnumerable<SalaryComponent>? components = null);

    /// <summary>
    /// Executes IVP on a Payroll record and items (gross/net calculations, period integrity, no duplicates).
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> VerifyPayrollRunAsync(Payroll payroll, IEnumerable<PayrollItem>? items = null);

    /// <summary>
    /// Executes IVP on a Bank Account CDI (format, active employee, primary account uniqueness).
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> VerifyBankAccountAsync(BankAccount account);

    /// <summary>
    /// Executes IVP on a Payment CDI (matches approved payroll net pay).
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> VerifyPaymentAsync(Payment payment);

    /// <summary>
    /// Persists IVP verification outcome in CLARK_WILSON_VERIFICATION_LOG.
    /// </summary>
    Task LogVerificationResultAsync(string ivpName, string targetEntity, int targetEntityId, bool isConsistent, string? errors = null);
}
