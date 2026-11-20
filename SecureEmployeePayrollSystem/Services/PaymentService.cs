using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class PaymentService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly ILogger<PaymentService> _logger;
    private readonly IIntegrityAssignmentService? _integrity;

    public PaymentService(
        PayrollDbContext context,
        AuditService audit,
        ILogger<PaymentService> logger,
        IIntegrityAssignmentService? integrity = null)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
        _integrity = integrity;
    }

    public async Task<(List<Payment> Items, int TotalCount)> GetPaymentsAsync(
        string? method,
        string? status,
        int page = 1,
        int pageSize = 15)
    {
        var query = _context.Payments
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.Payroll)
                    .ThenInclude(pr => pr.Employee)
                        .ThenInclude(e => e.Department)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(method))
        {
            query = query.Where(p => p.PaymentMethod == method);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.PaymentStatus == status);
        }

        int totalCount = await query.CountAsync();
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 5, 100);

        var items = await query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(bool Success, string Error, int Id)> RecordPaymentAsync(
        int payrollItemId,
        DateOnly paymentDate,
        decimal amount,
        string method,
        string transactionRef,
        string status,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(transactionRef))
            {
                transactionRef = "TXN-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();
            }

            bool refExists = await _context.Payments.AnyAsync(p => p.TransactionReference == transactionRef);
            if (refExists)
            {
                return (false, "Transaction reference already exists.", 0);
            }

            if (_integrity != null && currentUserId > 0)
            {
                var targetLevel = string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
                    ? IntegrityLevel.HighlyTrusted
                    : IntegrityLevel.Trusted;

                var subjectLevel = await _integrity.GetUserIntegrityLevelAsync(currentUserId);
                if (subjectLevel < targetLevel)
                {
                    await _integrity.RecordAuditAsync(
                        currentUserId,
                        "BIBA_WRITE_DENIED",
                        "Payment",
                        0,
                        subjectLevel,
                        targetLevel,
                        "DENIED",
                        $"Biba No Write Up: Subject level {subjectLevel} cannot issue or record a {targetLevel} payment.",
                        ipAddress);

                    return (false, $"Biba Integrity Violation: Lower-integrity subject cannot issue {targetLevel} payments.", 0);
                }
            }

            var payment = new Payment
            {
                PayrollItemId = payrollItemId,
                PaymentDate = paymentDate,
                Amount = amount,
                PaymentMethod = method,
                TransactionReference = transactionRef,
                PaymentStatus = status
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            if (_integrity != null && currentUserId > 0)
            {
                var targetLevel = string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
                    ? IntegrityLevel.HighlyTrusted
                    : IntegrityLevel.Trusted;

                await _integrity.PromoteObjectIntegrityAsync("Payment", payment.PaymentId, targetLevel, currentUserId, "Payment Voucher Recorded", transactionRef);
            }

            await _audit.LogAsync(
                currentUserId,
                "RECORD_PAYMENT",
                "PAYMENT",
                payment.PaymentId,
                "Success",
                $"Recorded payment voucher {transactionRef} for amount {amount:C} via {method}",
                ipAddress);

            return (true, string.Empty, payment.PaymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record payment.");
            return (false, ex.Message, 0);
        }
    }
}
