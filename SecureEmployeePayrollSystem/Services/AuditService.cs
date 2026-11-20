using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class AuditService
{
    private readonly PayrollDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(PayrollDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(
        int userId,
        string actionType,
        string tableName,
        int recordId,
        string actionStatus,
        string? description,
        string? ipAddress)
    {
        try
        {
            // Verify user exists to satisfy foreign key FK_AUDIT_LOG_USER
            bool userExists = await _context.UserAccounts.AnyAsync(u => u.UserId == userId);
            if (!userExists)
            {
                // If user doesn't exist, log to server log without throwing foreign key violation
                _logger.LogWarning("Audit log skipped for non-existent UserId {UserId}. Action: {ActionType}, Desc: {Description}",
                    userId, actionType, description);
                return;
            }

            var log = new AuditLog
            {
                UserId = userId,
                ActionType = actionType.Length > 30 ? actionType.Substring(0, 30) : actionType,
                TableName = tableName.Length > 128 ? tableName.Substring(0, 128) : tableName,
                RecordId = recordId,
                ActionStatus = actionStatus.Length > 30 ? actionStatus.Substring(0, 30) : actionStatus,
                Description = description != null && description.Length > 500 ? description.Substring(0, 500) : description,
                IpAddress = ipAddress != null && ipAddress.Length > 45 ? ipAddress.Substring(0, 45) : ipAddress,
                ActionDateTime = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log entry for User {UserId}, Action {ActionType}", userId, actionType);
        }
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(
        string? actionType,
        string? tableName,
        int? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(a => a.ActionType == actionType);
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            query = query.Where(a => a.TableName == tableName);
        }

        if (userId.HasValue && userId.Value > 0)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(a => a.ActionDateTime >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(a => a.ActionDateTime <= dateTo.Value);
        }

        int totalCount = await query.CountAsync();
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 5, 100);

        var items = await query
            .OrderByDescending(a => a.ActionDateTime)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
