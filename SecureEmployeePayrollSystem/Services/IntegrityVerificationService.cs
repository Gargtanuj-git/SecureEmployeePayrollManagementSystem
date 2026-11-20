using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

/// <summary>
/// Clark-Wilson Integrity Verification Procedures (IVPs) implementation.
/// Ensures Constrained Data Items (CDIs) satisfy valid business states before and after transformations.
/// </summary>
public class IntegrityVerificationService : IIntegrityVerificationService
{
    private readonly PayrollDbContext _context;
    private readonly ILogger<IntegrityVerificationService> _logger;

    public IntegrityVerificationService(
        PayrollDbContext context,
        ILogger<IntegrityVerificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, List<string> Errors)> VerifyEmployeeAsync(Employee employee)
    {
        var errors = new List<string>();

        if (employee == null)
        {
            errors.Add("Employee record cannot be null.");
            return (false, errors);
        }

        if (string.IsNullOrWhiteSpace(employee.FirstName))
            errors.Add("First name is mandatory.");
        if (string.IsNullOrWhiteSpace(employee.LastName))
            errors.Add("Last name is mandatory.");

        if (string.IsNullOrWhiteSpace(employee.Email) || !Regex.IsMatch(employee.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            errors.Add("A valid email address is mandatory.");
        }
        else
        {
            // Verify email uniqueness among other active employees
            var duplicate = await _context.Employees
                .AsNoTracking()
                .AnyAsync(e => e.Email.ToLower() == employee.Email.ToLower() && e.EmployeeId != employee.EmployeeId);
            if (duplicate)
            {
                errors.Add($"Email address '{employee.Email}' is already assigned to another employee.");
            }
        }

        if (employee.DepartmentId <= 0)
        {
            errors.Add("Employee must be assigned to a valid department.");
        }
        else
        {
            var dept = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == employee.DepartmentId);
            if (dept == null)
            {
                errors.Add($"Referenced Department #{employee.DepartmentId} does not exist.");
            }
        }

        if (employee.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)))
        {
            errors.Add("Employee must be at least 18 years of age.");
        }

        if (employee.HireDate < employee.DateOfBirth)
        {
            errors.Add("Hire date cannot precede employee date of birth.");
        }

        bool isValid = errors.Count == 0;
        await LogVerificationResultAsync("IVP_EMPLOYEE", "Employee", employee.EmployeeId, isValid, string.Join("; ", errors));
        return (isValid, errors);
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, List<string> Errors)> VerifySalaryStructureAsync(SalaryStructure structure, IEnumerable<SalaryComponent>? components = null)
    {
        var errors = new List<string>();

        if (structure == null)
        {
            errors.Add("Salary structure cannot be null.");
            return (false, errors);
        }

        if (string.IsNullOrWhiteSpace(structure.StructureName))
            errors.Add("Structure name is mandatory.");

        if (structure.BasicSalary <= 0)
            errors.Add("Basic salary must be greater than zero.");

        if (structure.BasicSalary > 10_000_000m)
            errors.Add("Basic salary exceeds maximum corporate threshold of ₹10,000,000.");

        if (structure.EffectiveTo.HasValue && structure.EffectiveTo.Value < structure.EffectiveFrom)
            errors.Add("Effective-to date cannot be earlier than effective-from date.");

        var activeComponents = components?.ToList() ?? await _context.SalaryComponents
            .AsNoTracking()
            .Where(c => c.SalaryStructureId == structure.SalaryStructureId)
            .ToListAsync();

        decimal totalDeductions = 0m;
        foreach (var c in activeComponents)
        {
            if (string.IsNullOrWhiteSpace(c.ComponentName))
                errors.Add("Component name is required.");

            if (c.CalculationType == "Fixed" && c.FixedAmount <= 0)
                errors.Add($"Component '{c.ComponentName}' must have a positive fixed amount.");

            if (c.CalculationType == "Percentage" && (!c.Percentage.HasValue || c.Percentage.Value <= 0 || c.Percentage.Value > 100))
                errors.Add($"Component '{c.ComponentName}' percentage must be between 0% and 100%.");

            if (c.ComponentType == "Deduction")
            {
                decimal amount = c.CalculationType == "Fixed" ? (c.FixedAmount ?? 0m) : (structure.BasicSalary * (c.Percentage ?? 0m) / 100m);
                totalDeductions += amount;
            }
        }

        if (totalDeductions > structure.BasicSalary * 0.70m)
        {
            errors.Add("Total salary deductions exceed statutory ceiling of 70% of basic salary.");
        }

        bool isValid = errors.Count == 0;
        await LogVerificationResultAsync("IVP_SALARY", "SalaryStructure", structure.SalaryStructureId, isValid, string.Join("; ", errors));
        return (isValid, errors);
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, List<string> Errors)> VerifyPayrollRunAsync(Payroll payroll, IEnumerable<PayrollItem>? items = null)
    {
        var errors = new List<string>();

        if (payroll == null)
        {
            errors.Add("Payroll record cannot be null.");
            return (false, errors);
        }

        if (payroll.EmployeeId <= 0)
        {
            errors.Add("Payroll must reference a valid employee ID.");
        }
        else
        {
            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == payroll.EmployeeId);
            if (emp == null)
            {
                errors.Add($"Referenced employee #{payroll.EmployeeId} does not exist.");
            }
        }

        if (payroll.PayPeriodEnd < payroll.PayPeriodStart)
        {
            errors.Add("Pay period end date cannot be earlier than period start date.");
        }

        if (payroll.GrossSalary < 0)
        {
            errors.Add("Gross salary cannot be negative.");
        }

        if (payroll.TotalDeductions < 0)
        {
            errors.Add("Total deductions cannot be negative.");
        }

        // Check calculation consistency: Net = Gross - Deductions
        decimal calculatedNet = payroll.GrossSalary - payroll.TotalDeductions;
        if (payroll.NetSalary.HasValue && Math.Abs(payroll.NetSalary.Value - calculatedNet) > 0.01m)
        {
            errors.Add($"Payroll net salary discrepancy: Net ({payroll.NetSalary.Value}) != Gross ({payroll.GrossSalary}) - Deductions ({payroll.TotalDeductions}).");
        }

        if (calculatedNet < 0)
        {
            errors.Add($"Net salary cannot be negative (Computed: {calculatedNet}).");
        }

        // Prevent duplicate payroll for the same employee in the exact same pay period
        var isDuplicate = await _context.Payrolls
            .AsNoTracking()
            .AnyAsync(p => p.EmployeeId == payroll.EmployeeId
                        && p.PayPeriodStart == payroll.PayPeriodStart
                        && p.PayPeriodEnd == payroll.PayPeriodEnd
                        && p.PayrollId != payroll.PayrollId);
        if (isDuplicate)
        {
            errors.Add($"Duplicate payroll run detected for Employee #{payroll.EmployeeId} for period {payroll.PayPeriodStart} to {payroll.PayPeriodEnd}.");
        }

        bool isValid = errors.Count == 0;
        await LogVerificationResultAsync("IVP_PAYROLL", "Payroll", payroll.PayrollId, isValid, string.Join("; ", errors));
        return (isValid, errors);
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, List<string> Errors)> VerifyBankAccountAsync(BankAccount account)
    {
        var errors = new List<string>();

        if (account == null)
        {
            errors.Add("Bank account record cannot be null.");
            return (false, errors);
        }

        if (string.IsNullOrWhiteSpace(account.AccountNumber) || account.AccountNumber.Trim().Length < 8 || account.AccountNumber.Trim().Length > 24)
        {
            errors.Add("Bank account number must be between 8 and 24 characters.");
        }

        if (account.EmployeeId <= 0)
        {
            errors.Add("Bank account must be linked to a valid employee.");
        }
        else
        {
            var empExists = await _context.Employees.AsNoTracking().AnyAsync(e => e.EmployeeId == account.EmployeeId);
            if (!empExists)
            {
                errors.Add($"Employee #{account.EmployeeId} does not exist for this bank account.");
            }
        }

        // Enforce at most one primary account per employee
        if (account.IsPrimary)
        {
            var primaryDuplicate = await _context.BankAccounts
                .AsNoTracking()
                .AnyAsync(b => b.EmployeeId == account.EmployeeId && b.IsPrimary && b.BankAccountId != account.BankAccountId);
            if (primaryDuplicate)
            {
                errors.Add($"Employee #{account.EmployeeId} already has an active primary bank account.");
            }
        }

        bool isValid = errors.Count == 0;
        await LogVerificationResultAsync("IVP_BANK_ACCOUNT", "BankAccount", account.BankAccountId, isValid, string.Join("; ", errors));
        return (isValid, errors);
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, List<string> Errors)> VerifyPaymentAsync(Payment payment)
    {
        var errors = new List<string>();

        if (payment == null)
        {
            errors.Add("Payment record cannot be null.");
            return (false, errors);
        }

        if (payment.Amount <= 0)
        {
            errors.Add("Payment amount must be greater than zero.");
        }

        var payrollItem = await _context.PayrollItems
            .Include(pi => pi.Payroll)
            .AsNoTracking()
            .FirstOrDefaultAsync(pi => pi.PayrollItemId == payment.PayrollItemId);

        if (payrollItem == null || payrollItem.Payroll == null)
        {
            errors.Add($"Referenced Payroll Item #{payment.PayrollItemId} or its parent Payroll does not exist.");
        }
        else
        {
            var payroll = payrollItem.Payroll;
            if (payroll.PayrollStatus != "Approved" && payroll.PayrollStatus != "Paid")
            {
                errors.Add($"Cannot disburse payment for unapproved payroll #{payroll.PayrollId} (Current Status: {payroll.PayrollStatus}).");
            }

            decimal expectedNet = payroll.NetSalary ?? (payroll.GrossSalary - payroll.TotalDeductions);
            if (Math.Abs(payment.Amount - expectedNet) > 0.01m)
            {
                errors.Add($"Payment amount ({payment.Amount:C}) does not match approved payroll net salary ({expectedNet:C}).");
            }
        }

        bool isValid = errors.Count == 0;
        await LogVerificationResultAsync("IVP_PAYMENT", "Payment", payment.PaymentId, isValid, string.Join("; ", errors));
        return (isValid, errors);
    }

    /// <inheritdoc />
    public async Task LogVerificationResultAsync(string ivpName, string targetEntity, int targetEntityId, bool isConsistent, string? errors = null)
    {
        try
        {
            var log = new ClarkWilsonVerificationLog
            {
                IvpName = ivpName,
                TargetEntity = targetEntity,
                TargetEntityId = targetEntityId,
                IsConsistent = isConsistent,
                ValidationErrors = errors,
                VerifiedAtUtc = DateTime.UtcNow
            };

            _context.ClarkWilsonVerificationLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist Clark-Wilson IVP log for {IvpName} on {Entity} #{Id}", ivpName, targetEntity, targetEntityId);
        }
    }
}
