using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly AccountAuthService _authService;
    private readonly PasswordHasher<UserAccount> _hasher;

    public SettingsController(
        PayrollDbContext context,
        AuditService audit,
        AccountAuthService authService)
    {
        _context = context;
        _audit = audit;
        _authService = authService;
        _hasher = new PasswordHasher<UserAccount>();
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int userId = GetCurrentUserId();
        var user = await _context.UserAccounts
            .Include(u => u.Employee)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        ViewBag.UserAccount = user;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "New password must be at least 6 characters long.";
            return RedirectToAction(nameof(Index));
        }

        if (newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "New password and confirmation password do not match.";
            return RedirectToAction(nameof(Index));
        }

        int userId = GetCurrentUserId();
        var user = await _context.UserAccounts.FindAsync(userId);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Verify old password
        var (authSuccess, _, _) = await _authService.AuthenticateAsync(user.Username, currentPassword, false);
        if (!authSuccess)
        {
            TempData["ErrorMessage"] = "Incorrect current password.";
            return RedirectToAction(nameof(Index));
        }

        // Update with new password
        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.FailedLoginAttempts = 0;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            userId,
            "CHANGE_PASSWORD",
            "USER_ACCOUNT",
            user.UserId,
            "Success",
            $"User {user.Username} successfully changed their account password.",
            GetIpAddress());

        TempData["SuccessMessage"] = "Your password has been changed successfully.";
        return RedirectToAction(nameof(Index));
    }
}
