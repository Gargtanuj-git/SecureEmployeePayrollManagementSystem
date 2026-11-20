using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize(Policy = "StaffOrAdmin")]
public class ReportsController : Controller
{
    private readonly ReportService _reportService;
    private readonly PayrollDbContext _context;

    public ReportsController(ReportService reportService, PayrollDbContext context)
    {
        _reportService = reportService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var departments = await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync();
        ViewBag.Departments = departments;
        return View();
    }

    // ============================================================
    // EXPORT EMPLOYEES CSV
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> ExportEmployees(int? departmentId, string? status)
    {
        var employees = await _reportService.GetEmployeeReportAsync(departmentId, status);
        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine("Employee ID,First Name,Last Name,Gender,Date of Birth,Email,Phone,Department,Designation,Salary Grade,Hire Date,Status");

        foreach (var e in employees)
        {
            sb.AppendLine(string.Join(",",
                e.EmployeeId,
                ReportService.SanitizeCsvField(e.FirstName),
                ReportService.SanitizeCsvField(e.LastName),
                ReportService.SanitizeCsvField(e.Gender),
                e.DateOfBirth.ToString("yyyy-MM-dd"),
                ReportService.SanitizeCsvField(e.Email),
                ReportService.SanitizeCsvField(e.Phone),
                ReportService.SanitizeCsvField(e.Department?.DepartmentName),
                ReportService.SanitizeCsvField(e.Designation?.DesignationName),
                ReportService.SanitizeCsvField(e.SalaryStructure?.StructureName),
                e.HireDate.ToString("yyyy-MM-dd"),
                ReportService.SanitizeCsvField(e.EmploymentStatus)
            ));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fullBytes = preamble.Concat(bytes).ToArray();

        return File(fullBytes, "text/csv", $"SEPS_Employees_Report_{DateTime.Today:yyyyMMdd}.csv");
    }

    // ============================================================
    // EXPORT ATTENDANCE CSV
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> ExportAttendance(string? startStr, string? endStr, int? departmentId)
    {
        DateOnly? start = DateOnly.TryParse(startStr, out var s) ? s : null;
        DateOnly? end = DateOnly.TryParse(endStr, out var ed) ? ed : null;

        var attendances = await _reportService.GetAttendanceReportAsync(start, end, departmentId);
        var sb = new StringBuilder();

        sb.AppendLine("Attendance ID,Date,Employee ID,Employee Name,Department,Check In,Check Out,Working Hours,Overtime Hours,Status");

        foreach (var a in attendances)
        {
            sb.AppendLine(string.Join(",",
                a.AttendanceId,
                a.AttendanceDate.ToString("yyyy-MM-dd"),
                a.EmployeeId,
                ReportService.SanitizeCsvField($"{a.Employee?.FirstName} {a.Employee?.LastName}"),
                ReportService.SanitizeCsvField(a.Employee?.Department?.DepartmentName),
                a.CheckInTime?.ToString(@"hh\:mm") ?? "",
                a.CheckOutTime?.ToString(@"hh\:mm") ?? "",
                a.WorkingHours?.ToString("0.0") ?? "0.0",
                a.OvertimeHours?.ToString("0.0") ?? "0.0",
                ReportService.SanitizeCsvField(a.AttendanceStatus)
            ));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fullBytes = preamble.Concat(bytes).ToArray();

        return File(fullBytes, "text/csv", $"SEPS_Attendance_Report_{DateTime.Today:yyyyMMdd}.csv");
    }

    // ============================================================
    // EXPORT LEAVE CSV
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> ExportLeave(string? startStr, string? endStr, string? status)
    {
        DateOnly? start = DateOnly.TryParse(startStr, out var s) ? s : null;
        DateOnly? end = DateOnly.TryParse(endStr, out var ed) ? ed : null;

        var leaves = await _reportService.GetLeaveReportAsync(start, end, status);
        var sb = new StringBuilder();

        sb.AppendLine("Leave ID,Employee ID,Employee Name,Department,Leave Type,Start Date,End Date,Days,Status,Reason");

        foreach (var l in leaves)
        {
            sb.AppendLine(string.Join(",",
                l.LeaveId,
                l.EmployeeId,
                ReportService.SanitizeCsvField($"{l.Employee?.FirstName} {l.Employee?.LastName}"),
                ReportService.SanitizeCsvField(l.Employee?.Department?.DepartmentName),
                ReportService.SanitizeCsvField(l.LeaveType),
                l.StartDate.ToString("yyyy-MM-dd"),
                l.EndDate.ToString("yyyy-MM-dd"),
                l.NumberOfDays,
                ReportService.SanitizeCsvField(l.LeaveStatus),
                ReportService.SanitizeCsvField(l.Reason)
            ));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fullBytes = preamble.Concat(bytes).ToArray();

        return File(fullBytes, "text/csv", $"SEPS_Leave_Report_{DateTime.Today:yyyyMMdd}.csv");
    }

    // ============================================================
    // EXPORT PAYROLL CSV
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> ExportPayroll(string? startStr, string? endStr, string? status)
    {
        DateOnly? start = DateOnly.TryParse(startStr, out var s) ? s : null;
        DateOnly? end = DateOnly.TryParse(endStr, out var ed) ? ed : null;

        var payrolls = await _reportService.GetPayrollReportAsync(start, end, status);
        var sb = new StringBuilder();

        sb.AppendLine("Payroll ID,Employee ID,Employee Name,Department,Pay Period Start,Pay Period End,Gross Salary,Total Deductions,Net Salary,Status,Generated Date");

        foreach (var p in payrolls)
        {
            decimal net = p.NetSalary ?? (p.GrossSalary - p.TotalDeductions);
            sb.AppendLine(string.Join(",",
                p.PayrollId,
                p.EmployeeId,
                ReportService.SanitizeCsvField($"{p.Employee?.FirstName} {p.Employee?.LastName}"),
                ReportService.SanitizeCsvField(p.Employee?.Department?.DepartmentName),
                p.PayPeriodStart.ToString("yyyy-MM-dd"),
                p.PayPeriodEnd.ToString("yyyy-MM-dd"),
                p.GrossSalary.ToString("0.00"),
                p.TotalDeductions.ToString("0.00"),
                net.ToString("0.00"),
                ReportService.SanitizeCsvField(p.PayrollStatus),
                p.GeneratedDate.ToString("yyyy-MM-dd")
            ));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fullBytes = preamble.Concat(bytes).ToArray();

        return File(fullBytes, "text/csv", $"SEPS_Payroll_Report_{DateTime.Today:yyyyMMdd}.csv");
    }

    // ============================================================
    // EXPORT PAYMENTS CSV
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> ExportPayments(string? startStr, string? endStr, string? status)
    {
        DateOnly? start = DateOnly.TryParse(startStr, out var s) ? s : null;
        DateOnly? end = DateOnly.TryParse(endStr, out var ed) ? ed : null;

        var payments = await _reportService.GetPaymentReportAsync(start, end, status);
        var sb = new StringBuilder();

        sb.AppendLine("Payment ID,Payment Date,Payroll Slip ID,Employee Name,Method,Transaction Reference,Amount,Status");

        foreach (var p in payments)
        {
            var emp = p.PayrollItem?.Payroll?.Employee;
            sb.AppendLine(string.Join(",",
                p.PaymentId,
                p.PaymentDate.ToString("yyyy-MM-dd"),
                p.PayrollItem?.PayrollId ?? 0,
                ReportService.SanitizeCsvField(emp != null ? $"{emp.FirstName} {emp.LastName}" : "N/A"),
                ReportService.SanitizeCsvField(p.PaymentMethod),
                ReportService.SanitizeCsvField(p.TransactionReference),
                p.Amount.ToString("0.00"),
                ReportService.SanitizeCsvField(p.PaymentStatus)
            ));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fullBytes = preamble.Concat(bytes).ToArray();

        return File(fullBytes, "text/csv", $"SEPS_Payments_Report_{DateTime.Today:yyyyMMdd}.csv");
    }
}
