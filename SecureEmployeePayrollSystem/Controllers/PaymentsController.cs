using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize(Policy = "PayrollOrAdmin")]
public class PaymentsController : Controller
{
    private readonly PaymentService _paymentService;
    private readonly PayrollDbContext _context;

    public PaymentsController(PaymentService paymentService, PayrollDbContext context)
    {
        _paymentService = paymentService;
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ============================================================
    // PAYMENT VOUCHER REGISTER
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? method,
        string? status,
        int page = 1)
    {
        var (items, totalCount) = await _paymentService.GetPaymentsAsync(
            method,
            status,
            page,
            15);

        // Fetch pending/processed payroll items that can be paid (one representative item per payroll slip)
        var unpaidPayrollItems = await _context.PayrollItems
            .Include(pi => pi.Payroll)
                .ThenInclude(p => p.Employee)
            .Where(pi => pi.ItemType == "Earning" && pi.Payroll != null && pi.Payroll.Employee != null)
            .OrderByDescending(pi => pi.Payroll.PayPeriodEnd)
            .ThenByDescending(pi => pi.PayrollId)
            .ToListAsync();

        var distinctSlipItems = unpaidPayrollItems
            .GroupBy(pi => pi.PayrollId)
            .Select(g => g.First())
            .ToList();

        ViewBag.Method = method;
        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / 15);
        ViewBag.PayrollItems = distinctSlipItems;

        return View(items);
    }

    // ============================================================
    // RECORD PAYMENT (Payroll / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PayrollOrAdmin")]
    public async Task<IActionResult> Create(
        int payrollItemId,
        string paymentDate,
        decimal amount,
        string method,
        string? transactionRef,
        string status)
    {
        if (!DateOnly.TryParse(paymentDate, out var date))
        {
            TempData["ErrorMessage"] = "Invalid payment date.";
            return RedirectToAction(nameof(Index));
        }

        if (amount <= 0)
        {
            TempData["ErrorMessage"] = "Disbursement amount must be greater than zero.";
            return RedirectToAction(nameof(Index));
        }

        var (success, error, id) = await _paymentService.RecordPaymentAsync(
            payrollItemId,
            date,
            amount,
            method,
            transactionRef ?? string.Empty,
            status,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Payment voucher #{id} recorded successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}
