using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize(Policy = "AdminOnly")]
public class UserManagementController : Controller
{
    private readonly UserManagementService _userService;
    private readonly PayrollDbContext _context;
    private readonly MfaService _mfaService;
    private readonly IIntegrityAssignmentService? _integrity;

    public UserManagementController(
        UserManagementService userService,
        PayrollDbContext context,
        MfaService mfaService,
        IIntegrityAssignmentService? integrity = null)
    {
        _userService = userService;
        _context = context;
        _mfaService = mfaService;
        _integrity = integrity;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ============================================================
    // USER ACCOUNTS DIRECTORY
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        int? roleId,
        int page = 1)
    {
        var (items, totalCount) = await _userService.GetUsersAsync(
            search,
            status,
            roleId,
            page,
            15);

        var roles = await _context.AppRoles.OrderBy(r => r.RoleName).ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.RoleId = roleId;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / 15);
        ViewBag.Roles = roles;

        return View(items);
    }

    // ============================================================
    // CREATE USER ACCOUNT
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Roles = await _context.AppRoles.OrderBy(r => r.RoleName).ToListAsync();
        ViewBag.Employees = await _context.Employees
            .Include(e => e.Department)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
        ViewBag.Departments = await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync();

        return View(new SecureEmployeePayrollSystem.ViewModels.CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SecureEmployeePayrollSystem.ViewModels.CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Roles = await _context.AppRoles.OrderBy(r => r.RoleName).ToListAsync();
            ViewBag.Employees = await _context.Employees
                .Include(e => e.Department)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync();
            return View(model);
        }

        var (success, error, userId) = await _userService.CreateUserAccountAsync(
            model,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            ViewBag.Roles = await _context.AppRoles.OrderBy(r => r.RoleName).ToListAsync();
            ViewBag.Employees = await _context.Employees
                .Include(e => e.Department)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = $"User account '{model.Username}' was successfully created with assigned role.";
        return RedirectToAction(nameof(Details), new { id = userId });
    }

    // ============================================================
    // USER ACCOUNT DETAILS & SECURITY PROFILE
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var allRoles = await _context.AppRoles.OrderBy(r => r.RoleName).ToListAsync();
        ViewBag.AllRoles = allRoles;

        if (_integrity != null)
        {
            ViewBag.UserIntegrityLevel = await _integrity.GetUserIntegrityLevelAsync(id);
        }

        return View(user);
    }

    // ============================================================
    // BIBA INTEGRITY CLASSIFICATION UPDATE
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateIntegrityLevel(int userId, IntegrityLevel newLevel, string reason)
    {
        if (_integrity == null)
        {
            TempData["ErrorMessage"] = "Integrity service unavailable.";
            return RedirectToAction(nameof(Details), new { id = userId });
        }

        var (success, message) = await _integrity.AssignUserIntegrityAsync(
            userId,
            newLevel,
            GetCurrentUserId(),
            reason);

        if (!success)
        {
            TempData["ErrorMessage"] = message;
        }
        else
        {
            TempData["SuccessMessage"] = message;
        }

        return RedirectToAction(nameof(Details), new { id = userId });
    }

    // ============================================================
    // UNLOCK ACCOUNT
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(int id)
    {
        var (success, error) = await _userService.UnlockAccountAsync(
            id,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "User account unlocked successfully.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ============================================================
    // TOGGLE ACTIVE / INACTIVE STATUS
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var (success, error) = await _userService.ToggleStatusAsync(
            id,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "User account status changed.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // ASSIGN ROLE
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(int userId, int roleId)
    {
        var (success, error) = await _userService.AssignRoleAsync(
            userId,
            roleId,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "Role assigned successfully.";
        }

        return RedirectToAction(nameof(Details), new { id = userId });
    }

    // ============================================================
    // REMOVE ROLE
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(int userId, int roleId)
    {
        var (success, error) = await _userService.RemoveRoleAsync(
            userId,
            roleId,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "Role removed from user.";
        }

        return RedirectToAction(nameof(Details), new { id = userId });
    }

    // ============================================================
    // RESET PASSWORD
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "New password must be at least 6 characters long.";
            return RedirectToAction(nameof(Details), new { id = userId });
        }

        var (success, error) = await _userService.ResetPasswordAsync(
            userId,
            newPassword,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "User password successfully reset.";
        }

        return RedirectToAction(nameof(Details), new { id = userId });
    }

    // ============================================================
    // RESET MFA (ADMIN ASSISTED)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetMfa(int userId)
    {
        var (success, error) = await _mfaService.AdminResetMfaAsync(
            userId,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "MFA configuration has been reset for this user. They will be required to re-enroll on their next sign in.";
        }

        return RedirectToAction(nameof(Details), new { id = userId });
    }
}
