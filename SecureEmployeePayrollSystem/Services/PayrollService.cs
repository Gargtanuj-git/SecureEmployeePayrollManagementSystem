using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class PayrollService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly ILogger<PayrollService> _logger;
    private readonly IIntegrityAssignmentService? _integrity;
    private readonly ITransformationProcedureService? _tp;
    private readonly ISeparationOfDutiesService? _sod;
    private readonly IIntegrityVerificationService? _ivp;

    public PayrollService(
        PayrollDbContext context,
        AuditService audit,
        ILogger<PayrollService> logger,
        IIntegrityAssignmentService? integrity = null,
        ITransformationProcedureService? tp = null,
        ISeparationOfDutiesService? sod = null,
        IIntegrityVerificationService? ivp = null)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
        _integrity = integrity;
        _tp = tp;
        _sod = sod;
        _ivp = ivp;
    }

    public async Task<(List<Payroll> Items, int TotalCount)> GetPayrollsAsync(
        int? employeeId,
        DateOnly? periodStart,
        DateOnly? periodEnd,
        string? status,
        int page = 1,
        int pageSize = 15)
    {
        var query = _context.Payrolls
            .Include(p => p.Employee)
                .ThenInclude(e => e.Department)
            .Include(p => p.Employee.Designation)
            .Include(p => p.PayrollItems)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(p => p.EmployeeId == employeeId.Value);
        }

        if (periodStart.HasValue)
        {
            query = query.Where(p => p.PayPeriodStart >= periodStart.Value);
        }

        if (periodEnd.HasValue)
        {
            query = query.Where(p => p.PayPeriodEnd <= periodEnd.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.PayrollStatus == status);
        }

        int totalCount = await query.CountAsync();
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 5, 100);

        var items = await query
            .OrderByDescending(p => p.PayPeriodEnd)
            .ThenBy(p => p.Employee.FirstName)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Payroll?> GetPayrollByIdAsync(int id)
    {
        return await _context.Payrolls
            .Include(p => p.Employee)
                .ThenInclude(e => e.Department)
            .Include(p => p.Employee.Designation)
            .Include(p => p.Employee.BankAccount)
            .Include(p => p.Employee.SalaryStructure)
                .ThenInclude(s => s.SalaryComponents)
            .Include(p => p.PayrollItems)
                .ThenInclude(pi => pi.Payments)
            .FirstOrDefaultAsync(p => p.PayrollId == id);
    }

    public async Task<(bool Success, string Error, int ProcessedCount, decimal TotalGross, decimal TotalNet)> ProcessPayrollRunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        int currentUserId,
        string? ipAddress)
    {
        if (periodEnd < periodStart)
        {
            return (false, "Pay period end date cannot be earlier than start date.", 0, 0, 0);
        }

        if (_integrity != null && currentUserId > 0)
        {
            var subjectLevel = await _integrity.GetUserIntegrityLevelAsync(currentUserId);
            if (subjectLevel < IntegrityLevel.Trusted)
            {
                await _integrity.RecordAuditAsync(
                    currentUserId,
                    "BIBA_WRITE_DENIED",
                    "Payroll",
                    0,
                    subjectLevel,
                    IntegrityLevel.Trusted,
                    "DENIED",
                    $"Biba No Write Up: Subject level {subjectLevel} cannot execute payroll calculation run.",
                    ipAddress);

                return (false, "Biba Integrity Violation: Lower-integrity subject cannot execute payroll calculation runs.", 0, 0, 0);
            }
        }

        // Fetch all active employees with their salary structures and components
        var employees = await _context.Employees
            .Include(e => e.SalaryStructure)
                .ThenInclude(s => s.SalaryComponents)
            .Where(e => e.EmploymentStatus == "Active")
            .ToListAsync();

        if (!employees.Any())
        {
            return (false, "No active employees found to process payroll.", 0, 0, 0);
        }

        // Check which employees already have a payroll record for this exact period
        var existingPayrollEmpIds = await _context.Payrolls
            .Where(p => p.PayPeriodStart == periodStart && p.PayPeriodEnd == periodEnd)
            .Select(p => p.EmployeeId)
            .ToListAsync();

        var eligibleEmployees = employees
            .Where(e => !existingPayrollEmpIds.Contains(e.EmployeeId))
            .ToList();

        if (!eligibleEmployees.Any())
        {
            return (false, "All active employees already have payroll processed for this period (" + periodStart + " to " + periodEnd + ").", 0, 0, 0);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            int processedCount = 0;
            decimal totalGross = 0;
            decimal totalNet = 0;
            var today = DateOnly.FromDateTime(DateTime.Now);

            foreach (var emp in eligibleEmployees)
            {
                if (emp.SalaryStructure == null)
                {
                    _logger.LogWarning("Employee {EmpId} has no assigned Salary Structure. Skipping.", emp.EmployeeId);
                    continue;
                }

                decimal basic = emp.SalaryStructure.BasicSalary;
                decimal earningsTotal = basic;
                decimal deductionsTotal = 0;

                var itemsToCreate = new List<PayrollItem>
                {
                    new PayrollItem
                    {
                        Quantity = 1.00m,
                        CalculationBasis = basic,
                        ItemType = "Earning" // Basic Salary Item
                    }
                };

                foreach (var comp in emp.SalaryStructure.SalaryComponents)
                {
                    decimal amount = 0;
                    if (comp.CalculationType == "Percentage" && comp.Percentage.HasValue)
                    {
                        amount = Math.Round(basic * (comp.Percentage.Value / 100m), 2, MidpointRounding.AwayFromZero);
                    }
                    else if (comp.CalculationType == "Fixed" && comp.FixedAmount.HasValue)
                    {
                        amount = comp.FixedAmount.Value;
                    }

                    if (comp.ComponentType == "Earning")
                    {
                        earningsTotal += amount;
                        itemsToCreate.Add(new PayrollItem
                        {
                            Quantity = 1.00m,
                            CalculationBasis = amount,
                            ItemType = "Earning"
                        });
                    }
                    else if (comp.ComponentType == "Deduction")
                    {
                        deductionsTotal += amount;
                        itemsToCreate.Add(new PayrollItem
                        {
                            Quantity = 1.00m,
                            CalculationBasis = amount,
                            ItemType = "Deduction"
                        });
                    }
                }

                var payroll = new Payroll
                {
                    EmployeeId = emp.EmployeeId,
                    PayPeriodStart = periodStart,
                    PayPeriodEnd = periodEnd,
                    GrossSalary = earningsTotal,
                    TotalDeductions = deductionsTotal,
                    PayrollStatus = "Generated",
                    GeneratedDate = today
                };

                _context.Payrolls.Add(payroll);
                await _context.SaveChangesAsync();

                foreach (var item in itemsToCreate)
                {
                    item.PayrollId = payroll.PayrollId;
                    _context.PayrollItems.Add(item);
                }

                await _context.SaveChangesAsync();

                if (_integrity != null && currentUserId > 0)
                {
                    await _integrity.PromoteObjectIntegrityAsync("Payroll", payroll.PayrollId, IntegrityLevel.Trusted, currentUserId, "Payroll Run Calculation Generated", null);
                }

                processedCount++;
                totalGross += earningsTotal;
                totalNet += (earningsTotal - deductionsTotal);
            }

            await transaction.CommitAsync();

            await _audit.LogAsync(
                currentUserId,
                "PROCESS_PAYROLL",
                "PAYROLL",
                0,
                "Success",
                $"Processed payroll for {processedCount} employees for period {periodStart} to {periodEnd}. Total Gross: {totalGross:C}, Total Net: {totalNet:C}",
                ipAddress);

            return (true, string.Empty, processedCount, totalGross, totalNet);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to execute payroll processing transaction for period {Start} to {End}", periodStart, periodEnd);
            return (false, "Transaction failed: " + ex.Message, 0, 0, 0);
        }
    }

    public async Task<(bool Success, string Error)> UpdatePayrollStatusAsync(
        int payrollId,
        string newStatus,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            var payroll = await _context.Payrolls.Include(p => p.Employee).FirstOrDefaultAsync(p => p.PayrollId == payrollId);
            if (payroll == null) return (false, "Payroll record not found.");

            if (_integrity != null && currentUserId > 0)
            {
                var (allowed, reason, subjectLevel, objectLevel) = await _integrity.CheckWriteAccessAsync(currentUserId, "Payroll", payroll.PayrollId, ipAddress);
                if (!allowed)
                {
                    return (false, $"Biba Integrity Violation: {reason}");
                }

                bool isFinalizing = string.Equals(newStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(newStatus, "Finalized", StringComparison.OrdinalIgnoreCase);

                if (isFinalizing && subjectLevel < IntegrityLevel.HighlyTrusted)
                {
                    await _integrity.RecordAuditAsync(
                        currentUserId,
                        "BIBA_WRITE_DENIED",
                        "Payroll",
                        payroll.PayrollId,
                        subjectLevel,
                        IntegrityLevel.HighlyTrusted,
                        "DENIED",
                        $"Biba No Write Up: Subject level {subjectLevel} cannot approve or finalize payroll into HighlyTrusted tier.",
                        ipAddress);

                    return (false, "Biba Integrity Violation: Only HighlyTrusted administrators can approve or finalize payroll.");
                }
            }

            if (string.Equals(newStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                // Clark-Wilson Integrity Verification Procedure (IVP) check
                if (_ivp != null)
                {
                    var items = await _context.PayrollItems.Where(pi => pi.PayrollId == payroll.PayrollId).ToListAsync();
                    var (isValid, errors) = await _ivp.VerifyPayrollRunAsync(payroll, items);
                    if (!isValid)
                    {
                        var errMsg = string.Join("; ", errors);
                        if (_tp != null)
                        {
                            await _tp.RecordAuditAsync(currentUserId, "CW_IVP_FAILED", "Payroll", payroll.PayrollId, "REJECTED", $"IVP_PAYROLL failed: {errMsg}", ipAddress);
                        }
                        return (false, $"Clark-Wilson Integrity Verification Failed: {errMsg}");
                    }
                }

                // Clark-Wilson Separation of Duties (Dual Control) check
                if (_sod != null && _tp != null)
                {
                    var pendingTxn = await _context.ClarkWilsonTransactions
                        .Where(t => t.TargetEntityType == "Payroll" && t.TargetEntityId == payroll.PayrollId && t.Status == "Pending")
                        .OrderByDescending(t => t.TransactionId)
                        .FirstOrDefaultAsync();

                    if (pendingTxn != null)
                    {
                        var (allowed, sodReason) = await _sod.ValidateDualControlAsync(pendingTxn.TransactionId, currentUserId);
                        if (!allowed)
                        {
                            await _tp.RecordAuditAsync(currentUserId, "CW_SOD_VIOLATION", "Payroll", payroll.PayrollId, "REJECTED", sodReason, ipAddress);
                            return (false, $"Clark-Wilson Separation of Duties Violation: {sodReason}");
                        }

                        await _tp.ApproveTransactionAsync(pendingTxn.TransactionId, currentUserId, "Payroll approved via standard workflow", ipAddress);
                    }
                }
            }

            string oldStatus = payroll.PayrollStatus;
            payroll.PayrollStatus = newStatus;

            await _context.SaveChangesAsync();

            if (_integrity != null && currentUserId > 0)
            {
                bool isFinalizing = string.Equals(newStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(newStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(newStatus, "Finalized", StringComparison.OrdinalIgnoreCase);

                if (isFinalizing)
                {
                    await _integrity.PromoteObjectIntegrityAsync("Payroll", payroll.PayrollId, IntegrityLevel.HighlyTrusted, currentUserId, $"Status marked as {newStatus}", $"PAYROLL-{payrollId}");
                }
            }

            await _audit.LogAsync(
                currentUserId,
                "STATUS_CHANGE",
                "PAYROLL",
                payroll.PayrollId,
                "Success",
                $"Changed payroll #{payroll.PayrollId} status for Employee {payroll.Employee.FirstName} {payroll.Employee.LastName} from {oldStatus} to {newStatus}",
                ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update payroll status for {Id}", payrollId);
            return (false, ex.Message);
        }
    }
}
