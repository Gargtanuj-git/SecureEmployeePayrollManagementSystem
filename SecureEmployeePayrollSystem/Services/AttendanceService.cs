using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class AttendanceService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly ILogger<AttendanceService> _logger;
    private readonly IIntegrityAssignmentService? _integrity;

    public AttendanceService(
        PayrollDbContext context,
        AuditService audit,
        ILogger<AttendanceService> logger,
        IIntegrityAssignmentService? integrity = null)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
        _integrity = integrity;
    }

    public async Task<(List<Attendance> Items, int TotalCount)> GetAttendancesAsync(
        DateOnly? date,
        int? employeeId,
        int? departmentId,
        string? status,
        int page = 1,
        int pageSize = 15)
    {
        var query = _context.Attendances
            .Include(a => a.Employee)
                .ThenInclude(e => e.Department)
            .Include(a => a.Employee.Designation)
            .AsNoTracking()
            .AsQueryable();

        if (date.HasValue)
        {
            query = query.Where(a => a.AttendanceDate == date.Value);
        }

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        }

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(a => a.Employee.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.AttendanceStatus == status);
        }

        int totalCount = await query.CountAsync();
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 5, 100);

        var items = await query
            .OrderByDescending(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.FirstName)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(bool Success, string Error)> RecordAttendanceAsync(
        int employeeId,
        DateOnly date,
        TimeOnly? checkIn,
        TimeOnly? checkOut,
        string status,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            bool empExists = await _context.Employees.AnyAsync(e => e.EmployeeId == employeeId);
            if (!empExists) return (false, "Employee not found.");

            if (_integrity != null && currentUserId > 0)
            {
                var targetLevel = IntegrityLevel.Trusted;
                var subjectLevel = await _integrity.GetUserIntegrityLevelAsync(currentUserId);
                if (subjectLevel < targetLevel)
                {
                    await _integrity.RecordAuditAsync(
                        currentUserId,
                        "BIBA_WRITE_DENIED",
                        "Attendance",
                        0,
                        subjectLevel,
                        targetLevel,
                        "DENIED",
                        $"Biba No Write Up: Subject level {subjectLevel} cannot record or modify a Trusted attendance record.",
                        ipAddress);

                    return (false, "Biba Integrity Violation: Lower-integrity subject cannot directly write or modify Trusted attendance records.");
                }
            }

            if (checkIn.HasValue && checkOut.HasValue && checkOut.Value < checkIn.Value)
            {
                return (false, "Check-out time cannot be earlier than check-in time.");
            }

            var existing = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == date);

            if (existing != null)
            {
                existing.CheckInTime = checkIn;
                existing.CheckOutTime = checkOut;
                existing.AttendanceStatus = status;

                await _context.SaveChangesAsync();

                await _audit.LogAsync(currentUserId, "UPDATE", "ATTENDANCE", existing.AttendanceId, "Success",
                    $"Updated attendance for Employee ID {employeeId} on {date}: Status={status}", ipAddress);
            }
            else
            {
                var record = new Attendance
                {
                    EmployeeId = employeeId,
                    AttendanceDate = date,
                    CheckInTime = checkIn,
                    CheckOutTime = checkOut,
                    AttendanceStatus = status
                };

                _context.Attendances.Add(record);
                await _context.SaveChangesAsync();

                await _audit.LogAsync(currentUserId, "CREATE", "ATTENDANCE", record.AttendanceId, "Success",
                    $"Recorded attendance for Employee ID {employeeId} on {date}: Status={status}", ipAddress);
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record attendance for employee {Id} on {Date}", employeeId, date);
            return (false, "Failed to record attendance: " + ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> ClockInAsync(int employeeId, int currentUserId, string? ipAddress)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var nowTime = TimeOnly.FromDateTime(DateTime.Now);

        var existing = await _context.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == today);

        if (existing != null && existing.CheckInTime != null)
        {
            return (false, $"You have already checked in today at {existing.CheckInTime:HH:mm}.");
        }

        if (existing != null)
        {
            existing.CheckInTime = nowTime;
            existing.AttendanceStatus = "Present";
            await _context.SaveChangesAsync();
            await _audit.LogAsync(currentUserId, "CLOCK_IN", "ATTENDANCE", existing.AttendanceId, "Success",
                $"Employee {employeeId} clocked in at {nowTime:HH:mm}", ipAddress);
            return (true, string.Empty);
        }

        var record = new Attendance
        {
            EmployeeId = employeeId,
            AttendanceDate = today,
            CheckInTime = nowTime,
            AttendanceStatus = "Present"
        };

        _context.Attendances.Add(record);
        await _context.SaveChangesAsync();

        await _audit.LogAsync(currentUserId, "CLOCK_IN", "ATTENDANCE", record.AttendanceId, "Success",
            $"Employee {employeeId} clocked in at {nowTime:HH:mm}", ipAddress);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string Error)> ClockOutAsync(int employeeId, int currentUserId, string? ipAddress)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var nowTime = TimeOnly.FromDateTime(DateTime.Now);

        var record = await _context.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == today);

        if (record == null || record.CheckInTime == null)
        {
            return (false, "You must check in first before checking out.");
        }

        if (record.CheckOutTime != null)
        {
            return (false, $"You have already checked out today at {record.CheckOutTime:HH:mm}.");
        }

        record.CheckOutTime = nowTime;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(currentUserId, "CLOCK_OUT", "ATTENDANCE", record.AttendanceId, "Success",
            $"Employee {employeeId} clocked out at {nowTime:HH:mm}", ipAddress);

        return (true, string.Empty);
    }

    public async Task<(int TotalPresent, int TotalAbsent, int TotalOnLeave, int TotalHalfDay)> GetTodaySummaryAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var attendances = await _context.Attendances
            .Where(a => a.AttendanceDate == today)
            .ToListAsync();

        int present = attendances.Count(a => a.AttendanceStatus == "Present");
        int absent = attendances.Count(a => a.AttendanceStatus == "Absent");
        int onLeave = attendances.Count(a => a.AttendanceStatus == "Leave");
        int halfDay = attendances.Count(a => a.AttendanceStatus == "Half Day");

        return (present, absent, onLeave, halfDay);
    }
}
