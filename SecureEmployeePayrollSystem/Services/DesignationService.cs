using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class DesignationService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;

    public DesignationService(PayrollDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<List<Designation>> GetAllDesignationsAsync(string? search = null)
    {
        var query = _context.Designations
            .Include(d => d.Employees)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(d =>
                d.DesignationName.Contains(search) ||
                (d.JobLevel != null && d.JobLevel.Contains(search)) ||
                (d.Description != null && d.Description.Contains(search)));
        }

        return await query.OrderBy(d => d.DesignationName).ToListAsync();
    }

    public async Task<Designation?> GetByIdAsync(int id)
    {
        return await _context.Designations
            .Include(d => d.Employees)
                .ThenInclude(e => e.Department)
            .FirstOrDefaultAsync(d => d.DesignationId == id);
    }

    public async Task<(bool Success, string Error, int Id)> CreateAsync(Designation designation, int currentUserId, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(designation.DesignationName))
        {
            return (false, "Designation name is required.", 0);
        }

        bool exists = await _context.Designations.AnyAsync(d => d.DesignationName.ToLower() == designation.DesignationName.ToLower());
        if (exists)
        {
            return (false, "A designation with this name already exists.", 0);
        }

        _context.Designations.Add(designation);
        await _context.SaveChangesAsync();

        await _audit.LogAsync(currentUserId, "CREATE", "DESIGNATION", designation.DesignationId, "Success",
            $"Created designation {designation.DesignationName}", ipAddress);

        return (true, string.Empty, designation.DesignationId);
    }

    public async Task<(bool Success, string Error)> UpdateAsync(Designation designation, int currentUserId, string? ipAddress)
    {
        var existing = await _context.Designations.FindAsync(designation.DesignationId);
        if (existing == null) return (false, "Designation not found.");

        bool exists = await _context.Designations.AnyAsync(d =>
            d.DesignationId != designation.DesignationId &&
            d.DesignationName.ToLower() == designation.DesignationName.ToLower());
        if (exists)
        {
            return (false, "Another designation with this name already exists.");
        }

        existing.DesignationName = designation.DesignationName;
        existing.JobLevel = designation.JobLevel;
        existing.Description = designation.Description;

        await _context.SaveChangesAsync();

        await _audit.LogAsync(currentUserId, "UPDATE", "DESIGNATION", existing.DesignationId, "Success",
            $"Updated designation {existing.DesignationName}", ipAddress);

        return (true, string.Empty);
    }
}
