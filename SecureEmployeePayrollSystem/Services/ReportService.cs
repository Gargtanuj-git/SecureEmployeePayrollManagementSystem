using System;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class ReportService
{
    private readonly PayrollDbContext _context;

    public ReportService(PayrollDbContext context)
    {
        _context = context;
    }

    public async Task<List<Employee>> GetEmployeeReportAsync(int? departmentId, string? status)
    {
        var query = _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Designation)
            .Include(e => e.SalaryStructure)
            .AsNoTracking()
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.EmploymentStatus == status);
        }

        return await query.OrderBy(e => e.Department.DepartmentName).ThenBy(e => e.FirstName).ToListAsync();
    }

    public async Task<List<Attendance>> GetAttendanceReportAsync(DateOnly? startDate, DateOnly? endDate, int? departmentId)
    {
        var query = _context.Attendances
            .Include(a => a.Employee)
                .ThenInclude(e => e.Department)
            .Include(a => a.Employee.Designation)
            .AsNoTracking()
            .AsQueryable();

        if (startDate.HasValue) query = query.Where(a => a.AttendanceDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(a => a.AttendanceDate <= endDate.Value);
        if (departmentId.HasValue && departmentId.Value > 0) query = query.Where(a => a.Employee.DepartmentId == departmentId.Value);

        return await query.OrderByDescending(a => a.AttendanceDate).ThenBy(a => a.Employee.FirstName).ToListAsync();
    }

    public async Task<List<Leave>> GetLeaveReportAsync(DateOnly? startDate, DateOnly? endDate, string? status)
    {
        var query = _context.Leaves
            .Include(l => l.Employee)
                .ThenInclude(e => e.Department)
            .Include(l => l.Employee.Designation)
            .AsNoTracking()
            .AsQueryable();

        if (startDate.HasValue) query = query.Where(l => l.StartDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(l => l.EndDate <= endDate.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.LeaveStatus == status);

        return await query.OrderByDescending(l => l.StartDate).ToListAsync();
    }

    public async Task<List<Payroll>> GetPayrollReportAsync(DateOnly? startDate, DateOnly? endDate, string? status)
    {
        var query = _context.Payrolls
            .Include(p => p.Employee)
                .ThenInclude(e => e.Department)
            .Include(p => p.Employee.Designation)
            .AsNoTracking()
            .AsQueryable();

        if (startDate.HasValue) query = query.Where(p => p.PayPeriodStart >= startDate.Value);
        if (endDate.HasValue) query = query.Where(p => p.PayPeriodEnd <= endDate.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.PayrollStatus == status);

        return await query.OrderByDescending(p => p.PayPeriodEnd).ThenBy(p => p.Employee.FirstName).ToListAsync();
    }

    public async Task<List<Payment>> GetPaymentReportAsync(DateOnly? startDate, DateOnly? endDate, string? status)
    {
        var query = _context.Payments
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.Payroll)
                    .ThenInclude(pr => pr.Employee)
                        .ThenInclude(e => e.Department)
            .AsNoTracking()
            .AsQueryable();

        if (startDate.HasValue) query = query.Where(p => p.PaymentDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(p => p.PaymentDate <= endDate.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.PaymentStatus == status);

        return await query.OrderByDescending(p => p.PaymentDate).ToListAsync();
    }

    /// <summary>
    /// Sanitizes text against CSV formula injection (OWASP CSV Injection defense).
    /// </summary>
    public static string SanitizeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";

        string clean = field.Replace("\"", "\"\"");

        // If field begins with =, +, -, @, or tab/CR, prepend with single quote
        if (clean.StartsWith("=") || clean.StartsWith("+") || clean.StartsWith("-") || clean.StartsWith("@") || clean.StartsWith("\t") || clean.StartsWith("\r"))
        {
            clean = "'" + clean;
        }

        return $"\"{clean}\"";
    }
}
