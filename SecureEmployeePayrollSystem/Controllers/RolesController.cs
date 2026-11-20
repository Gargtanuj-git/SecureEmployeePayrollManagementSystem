using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize(Policy = "AdminOnly")]
public class RolesController : Controller
{
    private readonly PayrollDbContext _context;

    public RolesController(PayrollDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = await _context.AppRoles
            .Include(r => r.UserRoles)
            .OrderBy(r => r.RoleId)
            .ToListAsync();

        var rolePermissions = await _context.VwRolePermissions
            .OrderBy(rp => rp.RoleName)
            .ThenBy(rp => rp.PermissionName)
            .ToListAsync();

        ViewBag.RolePermissions = rolePermissions;

        return View(roles);
    }
}
