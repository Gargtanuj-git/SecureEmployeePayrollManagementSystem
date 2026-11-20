using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private readonly AttendanceService _attendanceService;
    private readonly PayrollDbContext _context;

    public AttendanceController(AttendanceService attendanceService, PayrollDbContext context)
    {
        _attendanceService = attendanceService;
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
    // ATTENDANCE LOGS
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? dateStr,
        int? departmentId,
        string? status,
        int page = 1)
    {
        DateOnly? date = null;
        if (!string.IsNullOrWhiteSpace(dateStr) && DateOnly.TryParse(dateStr, out var parsedDate))
        {
            date = parsedDate;
        }

        int? targetEmployeeId = null;
        bool isStaffOrManager = User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("Payroll Manager");
        
        if (!isStaffOrManager)
        {
            targetEmployeeId = GetCurrentEmployeeId();
        }

        var (items, totalCount) = await _attendanceService.GetAttendancesAsync(
            date,
            targetEmployeeId,
            departmentId,
            status,
            page,
            15);

        var summary = await _attendanceService.GetTodaySummaryAsync();
        var departments = await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync();
        var employees = await _context.Employees.Where(e => e.EmploymentStatus == "Active").OrderBy(e => e.FirstName).ToListAsync();

        // Check current user's today attendance for quick clock in/out
        Attendance? myTodayAttendance = null;
        var myEmpId = GetCurrentEmployeeId();
        if (myEmpId.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            myTodayAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == myEmpId.Value && a.AttendanceDate == today);
        }

        ViewBag.DateStr = dateStr;
        ViewBag.DepartmentId = departmentId;
        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / 15);
        ViewBag.Summary = summary;
        ViewBag.Departments = departments;
        ViewBag.Employees = employees;
        ViewBag.MyToday = myTodayAttendance;
        ViewBag.IsStaffOrManager = isStaffOrManager;

        return View(items);
    }

    // ============================================================
    // CLOCK IN
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClockIn()
    {
        var empId = GetCurrentEmployeeId();
        if (empId == null)
        {
            TempData["ErrorMessage"] = "No employee profile is linked to your user account.";
            return RedirectToAction(nameof(Index));
        }

        var (success, error) = await _attendanceService.ClockInAsync(empId.Value, GetCurrentUserId(), GetIpAddress());
        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Checked in successfully at {DateTime.Now:HH:mm}.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // CLOCK OUT
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClockOut()
    {
        var empId = GetCurrentEmployeeId();
        if (empId == null)
        {
            TempData["ErrorMessage"] = "No employee profile is linked to your user account.";
            return RedirectToAction(nameof(Index));
        }

        var (success, error) = await _attendanceService.ClockOutAsync(empId.Value, GetCurrentUserId(), GetIpAddress());
        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Checked out successfully at {DateTime.Now:HH:mm}.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // RECORD / UPDATE ATTENDANCE (HR / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Record(
        int employeeId,
        string attendanceDate,
        string? checkIn,
        string? checkOut,
        string status)
    {
        if (!DateOnly.TryParse(attendanceDate, out var date))
        {
            TempData["ErrorMessage"] = "Invalid date provided.";
            return RedirectToAction(nameof(Index));
        }

        TimeOnly? inTime = null;
        if (!string.IsNullOrWhiteSpace(checkIn) && TimeOnly.TryParse(checkIn, out var parsedIn))
        {
            inTime = parsedIn;
        }

        TimeOnly? outTime = null;
        if (!string.IsNullOrWhiteSpace(checkOut) && TimeOnly.TryParse(checkOut, out var parsedOut))
        {
            outTime = parsedOut;
        }

        var (success, error) = await _attendanceService.RecordAttendanceAsync(
            employeeId,
            date,
            inTime,
            outTime,
            status,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "Attendance record saved successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}
