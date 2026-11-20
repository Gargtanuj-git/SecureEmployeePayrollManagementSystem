using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class SalaryStructureService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly ILogger<SalaryStructureService> _logger;
    private readonly IIntegrityAssignmentService? _integrity;
    private readonly ITransformationProcedureService? _tp;
    private readonly ISeparationOfDutiesService? _sod;
    private readonly IIntegrityVerificationService? _ivp;

    public SalaryStructureService(
        PayrollDbContext context,
        AuditService audit,
        ILogger<SalaryStructureService> logger,
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

    public async Task<List<SalaryStructure>> GetAllStructuresAsync()
    {
        return await _context.SalaryStructures
            .Include(s => s.SalaryComponents)
            .Include(s => s.Employees)
            .OrderBy(s => s.StructureName)
            .ToListAsync();
    }

    public async Task<SalaryStructure?> GetByIdAsync(int id)
    {
        return await _context.SalaryStructures
            .Include(s => s.SalaryComponents)
            .Include(s => s.Employees)
                .ThenInclude(e => e.Department)
            .FirstOrDefaultAsync(s => s.SalaryStructureId == id);
    }

    public async Task<(bool Success, string Error, int Id)> CreateStructureAsync(
        SalaryStructure structure,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(structure.StructureName))
            {
                return (false, "Structure name is required.", 0);
            }

            if (structure.BasicSalary <= 0)
            {
                return (false, "Basic salary must be greater than zero.", 0);
            }

            if (string.IsNullOrWhiteSpace(structure.Status))
            {
                structure.Status = "Active";
            }

            if (_integrity != null && currentUserId > 0)
            {
                var targetLevel = string.Equals(structure.Status, "Active", StringComparison.OrdinalIgnoreCase)
                    ? IntegrityLevel.HighlyTrusted
                    : IntegrityLevel.SlightlyTrusted;

                var subjectLevel = await _integrity.GetUserIntegrityLevelAsync(currentUserId);
                if (subjectLevel < targetLevel)
                {
                    await _integrity.RecordAuditAsync(
                        currentUserId,
                        "BIBA_WRITE_DENIED",
                        "SalaryStructure",
                        0,
                        subjectLevel,
                        targetLevel,
                        "DENIED",
                        $"Biba No Write Up: Subject level {subjectLevel} cannot create a {targetLevel} salary structure.",
                        ipAddress);

                    return (false, $"Biba Integrity Violation: Lower-integrity subject cannot create a {targetLevel} salary structure.", 0);
                }
            }

            if (_ivp != null)
            {
                var (isValid, errors) = await _ivp.VerifySalaryStructureAsync(structure);
                if (!isValid)
                {
                    var errMsg = string.Join("; ", errors);
                    if (_tp != null)
                    {
                        await _tp.RecordAuditAsync(currentUserId, "CW_IVP_FAILED", "SalaryStructure", 0, "REJECTED", $"IVP_SALARY failed: {errMsg}", ipAddress);
                    }
                    return (false, $"Clark-Wilson Integrity Verification Failed: {errMsg}", 0);
                }
            }

            _context.SalaryStructures.Add(structure);
            await _context.SaveChangesAsync();

            if (_integrity != null && currentUserId > 0)
            {
                var targetLevel = string.Equals(structure.Status, "Active", StringComparison.OrdinalIgnoreCase)
                    ? IntegrityLevel.HighlyTrusted
                    : IntegrityLevel.SlightlyTrusted;

                await _integrity.PromoteObjectIntegrityAsync("SalaryStructure", structure.SalaryStructureId, targetLevel, currentUserId, "Initial Salary Structure Creation", null);
            }

            await _audit.LogAsync(currentUserId, "CREATE", "SALARY_STRUCTURE", structure.SalaryStructureId, "Success",
                $"Created Salary Structure {structure.StructureName} (Basic: {structure.BasicSalary:C})", ipAddress);

            return (true, string.Empty, structure.SalaryStructureId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create salary structure.");
            return (false, ex.Message, 0);
        }
    }

    public async Task<(bool Success, string Error)> UpdateStructureAsync(
        SalaryStructure structure,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            var existing = await _context.SalaryStructures.FindAsync(structure.SalaryStructureId);
            if (existing == null) return (false, "Salary structure not found.");

            if (_integrity != null && currentUserId > 0)
            {
                var (allowed, reason, _, _) = await _integrity.CheckWriteAccessAsync(currentUserId, "SalaryStructure", existing.SalaryStructureId, ipAddress);
                if (!allowed)
                {
                    return (false, $"Biba Integrity Violation: {reason}");
                }
            }

            if (_ivp != null)
            {
                var (isValid, errors) = await _ivp.VerifySalaryStructureAsync(structure);
                if (!isValid)
                {
                    var errMsg = string.Join("; ", errors);
                    if (_tp != null)
                    {
                        await _tp.RecordAuditAsync(currentUserId, "CW_IVP_FAILED", "SalaryStructure", structure.SalaryStructureId, "REJECTED", $"IVP_SALARY failed: {errMsg}", ipAddress);
                    }
                    return (false, $"Clark-Wilson Integrity Verification Failed: {errMsg}");
                }
            }

            existing.StructureName = structure.StructureName;
            existing.BasicSalary = structure.BasicSalary;
            existing.EffectiveFrom = structure.EffectiveFrom;
            existing.EffectiveTo = structure.EffectiveTo;
            existing.Status = structure.Status;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "UPDATE", "SALARY_STRUCTURE", existing.SalaryStructureId, "Success",
                $"Updated Salary Structure {existing.StructureName}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update salary structure {Id}", structure.SalaryStructureId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> AddComponentAsync(
        SalaryComponent component,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            if (_integrity != null && currentUserId > 0)
            {
                var (allowed, reason, _, _) = await _integrity.CheckWriteAccessAsync(currentUserId, "SalaryStructure", component.SalaryStructureId, ipAddress);
                if (!allowed)
                {
                    return (false, $"Biba Integrity Violation: {reason}");
                }
            }

            if (string.IsNullOrWhiteSpace(component.ComponentName))
            {
                return (false, "Component name is required.");
            }

            if (component.CalculationType == "Percentage" && (!component.Percentage.HasValue || component.Percentage.Value <= 0))
            {
                return (false, "Please provide a valid percentage greater than 0.");
            }

            if (component.CalculationType == "Fixed" && (!component.FixedAmount.HasValue || component.FixedAmount.Value <= 0))
            {
                return (false, "Please provide a valid fixed amount greater than 0.");
            }

            _context.SalaryComponents.Add(component);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "CREATE", "SALARY_COMPONENT", component.ComponentId, "Success",
                $"Added component {component.ComponentName} to structure #{component.SalaryStructureId}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add salary component.");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> DeleteComponentAsync(
        int componentId,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            var comp = await _context.SalaryComponents.FindAsync(componentId);
            if (comp == null) return (false, "Component not found.");

            if (_integrity != null && currentUserId > 0)
            {
                var (allowed, reason, _, _) = await _integrity.CheckWriteAccessAsync(currentUserId, "SalaryStructure", comp.SalaryStructureId, ipAddress);
                if (!allowed)
                {
                    return (false, $"Biba Integrity Violation: {reason}");
                }
            }

            _context.SalaryComponents.Remove(comp);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "DELETE", "SALARY_COMPONENT", componentId, "Success",
                $"Deleted component {comp.ComponentName} from structure #{comp.SalaryStructureId}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete component {Id}", componentId);
            return (false, ex.Message);
        }
    }
}
