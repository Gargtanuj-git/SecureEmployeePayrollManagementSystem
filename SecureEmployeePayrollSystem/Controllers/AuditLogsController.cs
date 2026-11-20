using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AuditLogsController : Controller
{
    private readonly AuditService _auditService;
    private readonly PayrollDbContext _context;

    public AuditLogsController(AuditService auditService, PayrollDbContext context)
    {
        _auditService = auditService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? actionType,
        string? tableName,
        int? userId,
        string? fromStr,
        string? toStr,
        int page = 1)
    {
        DateTime? dateFrom = null;
        if (!string.IsNullOrWhiteSpace(fromStr) && DateTime.TryParse(fromStr, out var df))
        {
            dateFrom = df;
        }

        DateTime? dateTo = null;
        if (!string.IsNullOrWhiteSpace(toStr) && DateTime.TryParse(toStr, out var dt))
        {
            dateTo = dt.Date.AddDays(1).AddTicks(-1);
        }

        var (items, totalCount) = await _auditService.GetAuditLogsAsync(
            actionType,
            tableName,
            userId,
            dateFrom,
            dateTo,
            page,
            20);

        var users = await _context.UserAccounts.OrderBy(u => u.Username).ToListAsync();

        ViewBag.ActionType = actionType;
        ViewBag.TableName = tableName;
        ViewBag.UserId = userId;
        ViewBag.FromStr = fromStr;
        ViewBag.ToStr = toStr;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / 20);
        ViewBag.Users = users;

        return View(items);
    }
}
