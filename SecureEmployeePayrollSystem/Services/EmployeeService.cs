using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class EmployeeService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly ILogger<EmployeeService> _logger;
    private readonly IIntegrityAssignmentService? _integrity;
    private readonly ITransformationProcedureService? _tp;
    private readonly ISeparationOfDutiesService? _sod;
    private readonly IIntegrityVerificationService? _ivp;

    public EmployeeService(
        PayrollDbContext context,
        AuditService audit,
        ILogger<EmployeeService> logger,
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

    public async Task<(List<Employee> Items, int TotalCount)> GetEmployeesAsync(
        string? search,
        int? departmentId,
        string? status,
        int page = 1,
        int pageSize = 10,
        string? sortColumn = "Name",
        bool sortAsc = true)
    {
        var query = _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Designation)
            .Include(e => e.SalaryStructure)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(e =>
                e.FirstName.Contains(search) ||
                e.LastName.Contains(search) ||
                e.Email.Contains(search) ||
                e.Phone.Contains(search));
        }

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.EmploymentStatus == status);
        }

        // Sorting
        query = (sortColumn?.ToLower(), sortAsc) switch
        {
            ("name", true) => query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName),
            ("name", false) => query.OrderByDescending(e => e.FirstName).ThenByDescending(e => e.LastName),
            ("email", true) => query.OrderBy(e => e.Email),
            ("email", false) => query.OrderByDescending(e => e.Email),
            ("department", true) => query.OrderBy(e => e.Department.DepartmentName),
            ("department", false) => query.OrderByDescending(e => e.Department.DepartmentName),
            ("hiredate", true) => query.OrderBy(e => e.HireDate),
            ("hiredate", false) => query.OrderByDescending(e => e.HireDate),
            ("status", true) => query.OrderBy(e => e.EmploymentStatus),
            ("status", false) => query.OrderByDescending(e => e.EmploymentStatus),
            _ => query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
        };

        int totalCount = await query.CountAsync();
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 5, 100);

        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Employee?> GetEmployeeByIdAsync(int id)
    {
        return await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Designation)
            .Include(e => e.SalaryStructure)
                .ThenInclude(s => s.SalaryComponents)
            .Include(e => e.BankAccount)
            .Include(e => e.UserAccount)
            .Include(e => e.Attendances.OrderByDescending(a => a.AttendanceDate).Take(10))
            .Include(e => e.Leaves.OrderByDescending(l => l.StartDate).Take(10))
            .Include(e => e.Payrolls.OrderByDescending(p => p.PayPeriodEnd).Take(6))
            .FirstOrDefaultAsync(e => e.EmployeeId == id);
    }

    public async Task<(bool Success, string Error, int EmployeeId)> CreateEmployeeAsync(
        Employee employee,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            // Validate unique email
            bool emailExists = await _context.Employees.AnyAsync(e => e.Email.ToLower() == employee.Email.ToLower());
            if (emailExists)
            {
                return (false, "An employee with this email address already exists.", 0);
            }

            // Ensure valid department, designation, salary structure
            bool deptExists = await _context.Departments.AnyAsync(d => d.DepartmentId == employee.DepartmentId);
            if (!deptExists) return (false, "Selected Department does not exist.", 0);

            bool desigExists = await _context.Designations.AnyAsync(d => d.DesignationId == employee.DesignationId);
            if (!desigExists) return (false, "Selected Designation does not exist.", 0);

            bool structExists = await _context.SalaryStructures.AnyAsync(s => s.SalaryStructureId == employee.SalaryStructureId);
            if (!structExists) return (false, "Selected Salary Structure does not exist.", 0);

            if (string.IsNullOrWhiteSpace(employee.EmploymentStatus))
            {
                employee.EmploymentStatus = "Active";
            }

            if (_integrity != null && currentUserId > 0)
            {
                var targetLevel = string.Equals(employee.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase)
                    ? IntegrityLevel.Trusted
                    : IntegrityLevel.SlightlyTrusted;

                var subjectLevel = await _integrity.GetUserIntegrityLevelAsync(currentUserId);
                if (subjectLevel < targetLevel)
                {
                    await _integrity.RecordAuditAsync(
                        currentUserId,
                        "BIBA_WRITE_DENIED",
                        "Employee",
                        0,
                        subjectLevel,
                        targetLevel,
                        "DENIED",
                        $"Biba No Write Up: Subject level {subjectLevel} cannot create a {targetLevel} employee record.",
                        ipAddress);

                    return (false, $"Biba Integrity Violation (No Write Up): Subject level {subjectLevel} cannot write to {targetLevel} employee records.", 0);
                }
            }

            if (_ivp != null)
            {
                var (isValid, errors) = await _ivp.VerifyEmployeeAsync(employee);
                if (!isValid)
                {
                    var errMsg = string.Join("; ", errors);
                    if (_tp != null)
                    {
                        await _tp.RecordAuditAsync(currentUserId, "CW_IVP_FAILED", "Employee", 0, "REJECTED", $"IVP_EMPLOYEE failed: {errMsg}", ipAddress);
                    }
                    return (false, $"Clark-Wilson Integrity Verification Failed: {errMsg}", 0);
                }
            }

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            // Automatically initialize baseline Payroll slip and Earning item so employee is immediately available across payroll & disbursements
            try
            {
                var salaryStructure = await _context.SalaryStructures
                    .Include(s => s.SalaryComponents)
                    .FirstOrDefaultAsync(s => s.SalaryStructureId == employee.SalaryStructureId);

                if (salaryStructure != null)
                {
                    var hire = employee.HireDate;
                    var periodStart = new DateOnly(hire.Year, hire.Month, 1);
                    var periodEnd = new DateOnly(hire.Year, hire.Month, DateTime.DaysInMonth(hire.Year, hire.Month));

                    decimal basic = salaryStructure.BasicSalary;
                    decimal earningsTotal = basic;
                    decimal deductionsTotal = 0;

                    var itemsToCreate = new List<PayrollItem>
                    {
                        new PayrollItem
                        {
                            Quantity = 1.00m,
                            CalculationBasis = basic,
                            ItemType = "Earning"
                        }
                    };

                    foreach (var comp in salaryStructure.SalaryComponents)
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

                    var initialPayroll = new Payroll
                    {
                        EmployeeId = employee.EmployeeId,
                        PayPeriodStart = periodStart,
                        PayPeriodEnd = periodEnd,
                        GrossSalary = earningsTotal,
                        TotalDeductions = deductionsTotal,
                        GeneratedDate = DateOnly.FromDateTime(DateTime.Now),
                        PayrollStatus = "Generated",
                        PayrollItems = itemsToCreate
                    };

                    _context.Payrolls.Add(initialPayroll);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception pEx)
            {
                _logger.LogWarning(pEx, "Initial payroll slip generation skipped for new employee {EmpId}", employee.EmployeeId);
            }

            if (_integrity != null && currentUserId > 0)
            {
                var targetLevel = string.Equals(employee.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase)
                    ? IntegrityLevel.Trusted
                    : IntegrityLevel.SlightlyTrusted;
                await _integrity.PromoteObjectIntegrityAsync("Employee", employee.EmployeeId, targetLevel, currentUserId, "Initial Employee Registration", null);
            }

            await _audit.LogAsync(
                currentUserId,
                "CREATE",
                "EMPLOYEE",
                employee.EmployeeId,
                "Success",
                $"Created employee {employee.FirstName} {employee.LastName} ({employee.Email})",
                ipAddress);

            return (true, string.Empty, employee.EmployeeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create employee.");
            return (false, "An error occurred while creating the employee: " + ex.Message, 0);
        }
    }

    public async Task<(bool Success, string Error)> UpdateEmployeeAsync(
        Employee updated,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            var existing = await _context.Employees.FindAsync(updated.EmployeeId);
            if (existing == null)
            {
                return (false, "Employee not found.");
            }

            if (_integrity != null && currentUserId > 0)
            {
                var (allowed, reason, _, _) = await _integrity.CheckWriteAccessAsync(currentUserId, "Employee", existing.EmployeeId, ipAddress);
                if (!allowed)
                {
                    return (false, $"Biba Integrity Violation: {reason}");
                }
            }

            if (_ivp != null)
            {
                var (isValid, errors) = await _ivp.VerifyEmployeeAsync(updated);
                if (!isValid)
                {
                    var errMsg = string.Join("; ", errors);
                    if (_tp != null)
                    {
                        await _tp.RecordAuditAsync(currentUserId, "CW_IVP_FAILED", "Employee", updated.EmployeeId, "REJECTED", $"IVP_EMPLOYEE failed: {errMsg}", ipAddress);
                    }
                    return (false, $"Clark-Wilson Integrity Verification Failed: {errMsg}");
                }
            }

            // Check if email changed and if duplicate
            if (!string.Equals(existing.Email, updated.Email, StringComparison.OrdinalIgnoreCase))
            {
                bool emailExists = await _context.Employees.AnyAsync(e =>
                    e.EmployeeId != updated.EmployeeId &&
                    e.Email.ToLower() == updated.Email.ToLower());
                if (emailExists)
                {
                    return (false, "An employee with this email address already exists.");
                }
            }

            existing.FirstName = updated.FirstName;
            existing.LastName = updated.LastName;
            existing.Email = updated.Email;
            existing.Phone = updated.Phone;
            existing.Address = updated.Address;
            existing.Gender = updated.Gender;
            existing.DateOfBirth = updated.DateOfBirth;
            existing.HireDate = updated.HireDate;
            existing.EmploymentStatus = updated.EmploymentStatus;
            existing.DepartmentId = updated.DepartmentId;
            existing.DesignationId = updated.DesignationId;
            existing.SalaryStructureId = updated.SalaryStructureId;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                currentUserId,
                "UPDATE",
                "EMPLOYEE",
                existing.EmployeeId,
                "Success",
                $"Updated employee {existing.FirstName} {existing.LastName}",
                ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update employee {Id}", updated.EmployeeId);
            return (false, "An error occurred while updating the employee: " + ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> ToggleStatusAsync(int id, int currentUserId, string? ipAddress)
    {
        try
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return (false, "Employee not found.");

            if (_integrity != null && currentUserId > 0)
            {
                var (allowed, reason, _, _) = await _integrity.CheckWriteAccessAsync(currentUserId, "Employee", employee.EmployeeId, ipAddress);
                if (!allowed)
                {
                    return (false, $"Biba Integrity Violation: {reason}");
                }
            }

            string oldStatus = employee.EmploymentStatus;
            employee.EmploymentStatus = oldStatus == "Active" ? "Inactive" : "Active";

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                currentUserId,
                "STATUS_CHANGE",
                "EMPLOYEE",
                employee.EmployeeId,
                "Success",
                $"Toggled status for employee {employee.FirstName} {employee.LastName} from {oldStatus} to {employee.EmploymentStatus}",
                ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle status for employee {Id}", id);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> UpdateBankAccountAsync(
        int employeeId,
        string accountNumber,
        string accountType,
        bool isPrimary,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            var emp = await _context.Employees.Include(e => e.BankAccount).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            if (emp == null) return (false, "Employee not found.");

            if (_integrity != null && currentUserId > 0)
            {
                int bankId = emp.BankAccount?.BankAccountId ?? 0;
                var targetLevel = isPrimary ? IntegrityLevel.HighlyTrusted : IntegrityLevel.SlightlyTrusted;
                var subjectLevel = await _integrity.GetUserIntegrityLevelAsync(currentUserId);

                if (subjectLevel < targetLevel)
                {
                    await _integrity.RecordAuditAsync(
                        currentUserId,
                        "BIBA_WRITE_DENIED",
                        "BankAccount",
                        bankId,
                        subjectLevel,
                        targetLevel,
                        "DENIED",
                        $"Biba No Write Up: Subject level {subjectLevel} cannot modify a {targetLevel} bank account.",
                        ipAddress);

                    return (false, $"Biba Integrity Violation: Lower-integrity subject cannot directly write to a {targetLevel} bank account.");
                }
            }

            if (_ivp != null)
            {
                var candidate = new BankAccount
                {
                    BankAccountId = emp.BankAccount?.BankAccountId ?? 0,
                    EmployeeId = employeeId,
                    AccountNumber = accountNumber.Trim(),
                    AccountType = accountType.Trim(),
                    RoleName = isPrimary ? "Primary" : "Secondary",
                    IsPrimary = isPrimary
                };
                var (isValid, errors) = await _ivp.VerifyBankAccountAsync(candidate);
                if (!isValid)
                {
                    var errMsg = string.Join("; ", errors);
                    if (_tp != null)
                    {
                        await _tp.RecordAuditAsync(currentUserId, "CW_IVP_FAILED", "BankAccount", candidate.BankAccountId, "REJECTED", $"IVP_BANK_ACCOUNT failed: {errMsg}", ipAddress);
                    }
                    return (false, $"Clark-Wilson Integrity Verification Failed: {errMsg}");
                }
            }

            if (emp.BankAccount == null)
            {
                emp.BankAccount = new BankAccount
                {
                    EmployeeId = employeeId,
                    AccountNumber = accountNumber.Trim(),
                    AccountType = accountType.Trim(),
                    RoleName = isPrimary ? "Primary" : "Secondary",
                    IsPrimary = isPrimary
                };
                _context.BankAccounts.Add(emp.BankAccount);
            }
            else
            {
                emp.BankAccount.AccountNumber = accountNumber.Trim();
                emp.BankAccount.AccountType = accountType.Trim();
                emp.BankAccount.RoleName = isPrimary ? "Primary" : "Secondary";
                emp.BankAccount.IsPrimary = isPrimary;
            }

            await _context.SaveChangesAsync();

            if (_integrity != null && currentUserId > 0)
            {
                var targetLevel = isPrimary ? IntegrityLevel.HighlyTrusted : IntegrityLevel.SlightlyTrusted;
                await _integrity.PromoteObjectIntegrityAsync("BankAccount", emp.BankAccount.BankAccountId, targetLevel, currentUserId, "Bank Account Information Updated", null);
            }

            await _audit.LogAsync(
                currentUserId,
                "UPDATE",
                "BANK_ACCOUNT",
                emp.BankAccount.BankAccountId,
                "Success",
                $"Updated bank account details for employee {emp.FirstName} {emp.LastName}",
                ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update bank account for employee {EmpId}", employeeId);
            return (false, "Failed to update bank account: " + ex.Message);
        }
    }
}
