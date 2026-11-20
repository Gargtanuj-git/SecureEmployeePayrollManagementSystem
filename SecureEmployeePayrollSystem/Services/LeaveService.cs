using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class LeaveService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly ILogger<LeaveService> _logger;
    private readonly IIntegrityAssignmentService? _integrity;
    private readonly ITransformationProcedureService? _tp;
    private readonly ISeparationOfDutiesService? _sod;
    private readonly IIntegrityVerificationService? _ivp;

    public LeaveService(
        PayrollDbContext context,
        AuditService audit,
        ILogger<LeaveService> logger,
        IIntegrityAssignmentService? integrity = null,
        ITransformationProcedureService? tp = null,
        ISeparationOfDutiesService? sod = null,
        IIntegrityVerificationService? ivp = null)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
        _integrity = integrity;
        _tp = tp;
        _sod = sod;
        _ivp = ivp;
    }

    public async Task<(List<Leave> Items, int TotalCount)> GetLeavesAsync(
        int? employeeId,
        string? status,
        string? type,
        int page = 1,
        int pageSize = 15)
    {
        var query = _context.Leaves
            .Include(l => l.Employee)
                .ThenInclude(e => e.Department)
            .Include(l => l.Employee.Designation)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(l => l.EmployeeId == employeeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.LeaveStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(l => l.LeaveType == type);
        }

        int totalCount = await query.CountAsync();
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 5, 100);

        var items = await query
            .OrderByDescending(l => l.StartDate)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(bool Success, string Error, int LeaveId)> ApplyLeaveAsync(
        int employeeId,
        string type,
        DateOnly start,
        DateOnly end,
        string? reason,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            if (end < start)
            {
                return (false, "End date cannot be earlier than start date.", 0);
            }

            bool empExists = await _context.Employees.AnyAsync(e => e.EmployeeId == employeeId);
            if (!empExists) return (false, "Employee not found.", 0);

            // Calculate number of business days (or calendar days)
            int totalDays = end.DayNumber - start.DayNumber + 1;
            if (totalDays <= 0)
            {
                return (false, "Invalid date range.", 0);
            }

            // Check for overlapping pending or approved leave
            bool overlap = await _context.Leaves.AnyAsync(l =>
                l.EmployeeId == employeeId &&
                (l.LeaveStatus == "Approved" || l.LeaveStatus == "Pending") &&
                l.StartDate <= end &&
                l.EndDate >= start);

            if (overlap)
            {
                return (false, "You already have a pending or approved leave overlapping these dates.", 0);
            }

            var leave = new Leave
            {
                EmployeeId = employeeId,
                LeaveType = type,
                StartDate = start,
                EndDate = end,
                NumberOfDays = (decimal)totalDays,
                LeaveStatus = "Pending",
                Reason = reason
            };

            _context.Leaves.Add(leave);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "APPLY", "LEAVE", leave.LeaveId, "Success",
                $"Employee {employeeId} applied for {totalDays} day(s) of {type} leave from {start} to {end}", ipAddress);

            return (true, string.Empty, leave.LeaveId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply leave for employee {Id}", employeeId);
            return (false, "Error submitting leave request: " + ex.Message, 0);
        }
    }

    public async Task<(bool Success, string Error)> ApproveLeaveAsync(int leaveId, int currentUserId, string? ipAddress)
    {
        try
        {
            var leave = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.LeaveId == leaveId);
            if (leave == null) return (false, "Leave request not found.");

            if (_integrity != null && currentUserId > 0)
            {
                var subjectLevel = await _integrity.GetUserIntegrityLevelAsync(currentUserId);
                if (subjectLevel < IntegrityLevel.Trusted)
                {
                    await _integrity.RecordAuditAsync(
                        currentUserId,
                        "BIBA_WRITE_DENIED",
                        "Leave",
                        leave.LeaveId,
                        subjectLevel,
                        IntegrityLevel.Trusted,
                        "DENIED",
                        $"Biba No Write Up: Subject level {subjectLevel} cannot approve a Trusted leave record.",
                        ipAddress);

                    return (false, "Biba Integrity Violation: Only Trusted managers or administrators can approve leave requests.");
                }
            }

            // Clark-Wilson Separation of Duties: Submitter != Approver
            if (_sod != null && currentUserId > 0)
            {
                var applicantUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.EmployeeId == leave.EmployeeId);
                if (applicantUser != null && applicantUser.UserId == currentUserId)
                {
                    if (_tp != null)
                    {
                        await _tp.RecordAuditAsync(currentUserId, "CW_SOD_VIOLATION", "Leave", leave.LeaveId, "REJECTED", "Separation of Duties violation: Employee cannot approve their own leave request.", ipAddress);
                    }
                    return (false, "Clark-Wilson Separation of Duties Violation: Employee cannot approve their own leave request.");
                }
            }

            if (leave.LeaveStatus != "Pending")
            {
                return (false, $"Leave request is already {leave.LeaveStatus}.");
            }

            leave.LeaveStatus = "Approved";
            leave.ApprovedDate = DateOnly.FromDateTime(DateTime.Now);

            await _context.SaveChangesAsync();

            if (_integrity != null && currentUserId > 0)
            {
                await _integrity.PromoteObjectIntegrityAsync("Leave", leave.LeaveId, IntegrityLevel.Trusted, currentUserId, "Leave Request Approved", null);
            }

            await _audit.LogAsync(currentUserId, "APPROVE", "LEAVE", leave.LeaveId, "Success",
                $"Approved leave #{leave.LeaveId} for employee {leave.Employee.FirstName} {leave.Employee.LastName}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve leave {Id}", leaveId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> RejectLeaveAsync(int leaveId, string? remarks, int currentUserId, string? ipAddress)
    {
        try
        {
            var leave = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.LeaveId == leaveId);
            if (leave == null) return (false, "Leave request not found.");

            if (leave.LeaveStatus != "Pending")
            {
                return (false, $"Leave request is already {leave.LeaveStatus}.");
            }

            leave.LeaveStatus = "Rejected";
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                leave.Reason = (leave.Reason + " [Rejected: " + remarks + "]").Trim();
                if (leave.Reason.Length > 250) leave.Reason = leave.Reason.Substring(0, 250);
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "REJECT", "LEAVE", leave.LeaveId, "Success",
                $"Rejected leave #{leave.LeaveId} for employee {leave.Employee.FirstName} {leave.Employee.LastName}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject leave {Id}", leaveId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> CancelLeaveAsync(int leaveId, int employeeId, int currentUserId, string? ipAddress)
    {
        try
        {
            var leave = await _context.Leaves.FirstOrDefaultAsync(l => l.LeaveId == leaveId);
            if (leave == null) return (false, "Leave request not found.");

            if (leave.EmployeeId != employeeId)
            {
                return (false, "You can only cancel your own leave requests.");
            }

            if (leave.LeaveStatus != "Pending")
            {
                return (false, "Only pending leave requests can be cancelled.");
            }

            leave.LeaveStatus = "Cancelled";
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "CANCEL", "LEAVE", leave.LeaveId, "Success",
                $"Cancelled leave #{leave.LeaveId} by Employee {employeeId}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel leave {Id}", leaveId);
            return (false, ex.Message);
        }
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _context.Leaves.CountAsync(l => l.LeaveStatus == "Pending");
    }
}
