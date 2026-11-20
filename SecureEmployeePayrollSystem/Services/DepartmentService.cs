using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class DepartmentService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly IIntegrityAssignmentService? _integrity;

    public DepartmentService(PayrollDbContext context, AuditService audit, IIntegrityAssignmentService? integrity = null)
    {
        _context = context;
        _audit = audit;
        _integrity = integrity;
    }

    public async Task<List<Department>> GetAllDepartmentsAsync(string? search = null)
    {
        var query = _context.Departments
            .Include(d => d.Employees)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(d =>
                d.DepartmentName.Contains(search) ||
                d.Location.Contains(search));
        }

        return await query.OrderBy(d => d.DepartmentName).ToListAsync();
    }

    public async Task<Department?> GetByIdAsync(int id)
    {
        return await _context.Departments
            .Include(d => d.Employees)
                .ThenInclude(e => e.Designation)
            .FirstOrDefaultAsync(d => d.DepartmentId == id);
    }

    public async Task<(bool Success, string Error, int Id)> CreateAsync(Department department, int currentUserId, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(department.DepartmentName))
        {
            return (false, "Department name is required.", 0);
        }

        bool exists = await _context.Departments.AnyAsync(d => d.DepartmentName.ToLower() == department.DepartmentName.ToLower());
        if (exists)
        {
            return (false, "A department with this name already exists.", 0);
        }

        if (string.IsNullOrWhiteSpace(department.DepartmentStatus))
        {
            department.DepartmentStatus = "Active";
        }

        if (_integrity != null && currentUserId > 0)
        {
            var targetLevel = IntegrityLevel.Trusted;
            var subjectLevel = await _integrity.GetUserIntegrityLevelAsync(currentUserId);
            if (subjectLevel < targetLevel)
            {
                await _integrity.RecordAuditAsync(
                    currentUserId,
                    "BIBA_WRITE_DENIED",
                    "Department",
                    0,
                    subjectLevel,
                    targetLevel,
                    "DENIED",
                    $"Biba No Write Up: Subject level {subjectLevel} cannot create a Trusted department.",
                    ipAddress);

                return (false, "Biba Integrity Violation: Lower-integrity subject cannot create a Trusted department.", 0);
            }
        }

        _context.Departments.Add(department);
        await _context.SaveChangesAsync();

        if (_integrity != null && currentUserId > 0)
        {
            await _integrity.PromoteObjectIntegrityAsync("Department", department.DepartmentId, IntegrityLevel.Trusted, currentUserId, "Initial Department Creation", null);
        }

        await _audit.LogAsync(currentUserId, "CREATE", "DEPARTMENT", department.DepartmentId, "Success",
            $"Created department {department.DepartmentName}", ipAddress);

        return (true, string.Empty, department.DepartmentId);
    }

    public async Task<(bool Success, string Error)> UpdateAsync(Department department, int currentUserId, string? ipAddress)
    {
        var existing = await _context.Departments.FindAsync(department.DepartmentId);
        if (existing == null) return (false, "Department not found.");

        if (_integrity != null && currentUserId > 0)
        {
            var (allowed, reason, _, _) = await _integrity.CheckWriteAccessAsync(currentUserId, "Department", existing.DepartmentId, ipAddress);
            if (!allowed)
            {
                return (false, $"Biba Integrity Violation: {reason}");
            }
        }

        bool exists = await _context.Departments.AnyAsync(d =>
            d.DepartmentId != department.DepartmentId &&
            d.DepartmentName.ToLower() == department.DepartmentName.ToLower());
        if (exists)
        {
            return (false, "Another department with this name already exists.");
        }

        existing.DepartmentName = department.DepartmentName;
        existing.Location = department.Location;
        existing.DepartmentStatus = department.DepartmentStatus;

        await _context.SaveChangesAsync();

        await _audit.LogAsync(currentUserId, "UPDATE", "DEPARTMENT", existing.DepartmentId, "Success",
            $"Updated department {existing.DepartmentName}", ipAddress);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string Error)> ToggleStatusAsync(int id, int currentUserId, string? ipAddress)
    {
        var existing = await _context.Departments.FindAsync(id);
        if (existing == null) return (false, "Department not found.");

        if (_integrity != null && currentUserId > 0)
        {
            var (allowed, reason, _, _) = await _integrity.CheckWriteAccessAsync(currentUserId, "Department", existing.DepartmentId, ipAddress);
            if (!allowed)
            {
                return (false, $"Biba Integrity Violation: {reason}");
            }
        }

        string oldStatus = existing.DepartmentStatus;
        existing.DepartmentStatus = oldStatus == "Active" ? "Inactive" : "Active";

        await _context.SaveChangesAsync();

        await _audit.LogAsync(currentUserId, "STATUS_CHANGE", "DEPARTMENT", existing.DepartmentId, "Success",
            $"Toggled status for department {existing.DepartmentName} from {oldStatus} to {existing.DepartmentStatus}", ipAddress);

        return (true, string.Empty);
    }
}
