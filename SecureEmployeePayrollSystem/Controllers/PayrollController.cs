using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize]
public class PayrollController : Controller
{
    private readonly PayrollService _payrollService;
    private readonly PayrollDbContext _context;
    private readonly IIntegrityAssignmentService? _integrity;

    public PayrollController(
        PayrollService payrollService,
        PayrollDbContext context,
        IIntegrityAssignmentService? integrity = null)
    {
        _payrollService = payrollService;
        _context = context;
        _integrity = integrity;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private int? GetCurrentEmployeeId()
    {
        var idClaim = User.FindFirst("EmployeeId")?.Value;
        return int.TryParse(idClaim, out int id) && id > 0 ? id : null;
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ============================================================
    // PAYROLL LIST / DIRECTORY
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? startStr,
        string? endStr,
        int? employeeId,
        string? status,
        int page = 1)
    {
        DateOnly? pStart = null;
        if (!string.IsNullOrWhiteSpace(startStr) && DateOnly.TryParse(startStr, out var ps))
        {
            pStart = ps;
        }

        DateOnly? pEnd = null;
        if (!string.IsNullOrWhiteSpace(endStr) && DateOnly.TryParse(endStr, out var pe))
        {
            pEnd = pe;
        }

        bool isStaffOrManager = User.IsInRole("Admin") || User.IsInRole("Payroll Manager");
        int? targetEmployeeId = employeeId;

        // If user is Employee only, restrict strictly to their own payrolls
        if (!isStaffOrManager)
        {
            targetEmployeeId = GetCurrentEmployeeId();
        }

        var (items, totalCount) = await _payrollService.GetPayrollsAsync(
            targetEmployeeId,
            pStart,
            pEnd,
            status,
            page,
            15);

        var employees = await _context.Employees
            .Where(e => e.EmploymentStatus == "Active")
            .OrderBy(e => e.FirstName)
            .ToListAsync();

        ViewBag.StartStr = startStr;
        ViewBag.EndStr = endStr;
        ViewBag.EmployeeId = employeeId;
        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / 15);
        ViewBag.IsStaffOrManager = isStaffOrManager;
        ViewBag.Employees = employees;

        return View(items);
    }

    // ============================================================
    // PROCESS MONTHLY PAYROLL RUN (Payroll / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PayrollOrAdmin")]
    public async Task<IActionResult> Process(string periodStart, string periodEnd)
    {
        if (!DateOnly.TryParse(periodStart, out var start) || !DateOnly.TryParse(periodEnd, out var end))
        {
            TempData["ErrorMessage"] = "Invalid date range specified for payroll processing.";
            return RedirectToAction(nameof(Index));
        }

        var (success, error, count, gross, net) = await _payrollService.ProcessPayrollRunAsync(
            start,
            end,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Payroll run generated successfully for {count} employee(s). Total Gross: ₹{gross:N2}, Net Pay: ₹{net:N2}.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // DETAILS
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (_integrity != null)
        {
            var (readAllowed, readReason, _, _) = await _integrity.CheckReadAccessAsync(GetCurrentUserId(), "Payroll", id, GetIpAddress());
            if (!readAllowed)
            {
                TempData["ErrorMessage"] = $"Biba Integrity Violation (No Read Down): {readReason}";
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        var payroll = await _payrollService.GetPayrollByIdAsync(id);
        if (payroll == null)
        {
            return NotFound();
        }

        // Authorization check: Employee can only view their own
        if (User.IsInRole("Employee") && !User.IsInRole("Admin") && !User.IsInRole("Payroll Manager"))
        {
            var myEmpId = GetCurrentEmployeeId();
            if (myEmpId == null || myEmpId.Value != payroll.EmployeeId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        return View(payroll);
    }

    // ============================================================
    // PAYSLIP (PRINTABLE VIEW)
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Payslip(int id)
    {
        if (_integrity != null)
        {
            var (readAllowed, readReason, _, _) = await _integrity.CheckReadAccessAsync(GetCurrentUserId(), "Payroll", id, GetIpAddress());
            if (!readAllowed)
            {
                TempData["ErrorMessage"] = $"Biba Integrity Violation (No Read Down): {readReason}";
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        var payroll = await _payrollService.GetPayrollByIdAsync(id);
        if (payroll == null)
        {
            return NotFound();
        }

        // Authorization check: Employee can only view their own
        if (User.IsInRole("Employee") && !User.IsInRole("Admin") && !User.IsInRole("Payroll Manager"))
        {
            var myEmpId = GetCurrentEmployeeId();
            if (myEmpId == null || myEmpId.Value != payroll.EmployeeId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        return View(payroll);
    }

    // ============================================================
    // UPDATE STATUS (Payroll / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PayrollOrAdmin")]
    public async Task<IActionResult> UpdateStatus(int id, string newStatus)
    {
        var (success, error) = await _payrollService.UpdatePayrollStatusAsync(
            id,
            newStatus,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Payroll #{id} status marked as '{newStatus}'.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
