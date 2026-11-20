using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize]
public class LeaveController : Controller
{
    private readonly LeaveService _leaveService;
    private readonly PayrollDbContext _context;

    public LeaveController(LeaveService leaveService, PayrollDbContext context)
    {
        _leaveService = leaveService;
        _context = context;
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
    // LEAVE DIRECTORY & QUEUE
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? status,
        string? type,
        int page = 1)
    {
        bool isStaffOrManager = User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("Payroll Manager");
        int? targetEmployeeId = null;

        if (!isStaffOrManager)
        {
            targetEmployeeId = GetCurrentEmployeeId();
        }

        var (items, totalCount) = await _leaveService.GetLeavesAsync(
            targetEmployeeId,
            status,
            type,
            page,
            15);

        // If manager, also get the pending approvals queue
        List<Leave> pendingQueue = new();
        if (isStaffOrManager)
        {
            pendingQueue = await _context.Leaves
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Employee.Designation)
                .Where(l => l.LeaveStatus == "Pending")
                .OrderBy(l => l.StartDate)
                .ToListAsync();
        }

        var employees = await _context.Employees
            .Where(e => e.EmploymentStatus == "Active")
            .OrderBy(e => e.FirstName)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Type = type;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / 15);
        ViewBag.IsStaffOrManager = isStaffOrManager;
        ViewBag.PendingQueue = pendingQueue;
        ViewBag.PendingCount = pendingQueue.Count;
        ViewBag.Employees = employees;
        ViewBag.MyEmployeeId = GetCurrentEmployeeId();

        return View(items);
    }

    // ============================================================
    // APPLY LEAVE
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(
        int? employeeId,
        string leaveType,
        string startDate,
        string endDate,
        string? reason)
    {
        int targetEmpId;
        bool isStaffOrManager = User.IsInRole("Admin") || User.IsInRole("HR Manager");

        if (isStaffOrManager && employeeId.HasValue && employeeId.Value > 0)
        {
            targetEmpId = employeeId.Value;
        }
        else
        {
            var myEmpId = GetCurrentEmployeeId();
            if (myEmpId == null)
            {
                TempData["ErrorMessage"] = "No employee profile is linked to your user account.";
                return RedirectToAction(nameof(Index));
            }
            targetEmpId = myEmpId.Value;
        }

        if (!DateOnly.TryParse(startDate, out var start) || !DateOnly.TryParse(endDate, out var end))
        {
            TempData["ErrorMessage"] = "Invalid date range specified.";
            return RedirectToAction(nameof(Index));
        }

        var (success, error, leaveId) = await _leaveService.ApplyLeaveAsync(
            targetEmpId,
            leaveType,
            start,
            end,
            reason,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Leave request submitted successfully (Application #{leaveId}).";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // APPROVE LEAVE (HR / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Approve(int id)
    {
        var (success, error) = await _leaveService.ApproveLeaveAsync(
            id,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Leave request #{id} approved successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // REJECT LEAVE (HR / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Reject(int id, string? remarks)
    {
        var (success, error) = await _leaveService.RejectLeaveAsync(
            id,
            remarks,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Leave request #{id} rejected.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // CANCEL LEAVE (Employee)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var myEmpId = GetCurrentEmployeeId();
        if (myEmpId == null)
        {
            TempData["ErrorMessage"] = "No employee profile linked.";
            return RedirectToAction(nameof(Index));
        }

        var (success, error) = await _leaveService.CancelLeaveAsync(
            id,
            myEmpId.Value,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Leave request #{id} cancelled.";
        }

        return RedirectToAction(nameof(Index));
    }
}
