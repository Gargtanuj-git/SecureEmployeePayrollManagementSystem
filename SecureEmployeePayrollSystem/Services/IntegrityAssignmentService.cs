using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class IntegrityAssignmentService : IIntegrityAssignmentService
{
    private readonly PayrollDbContext _context;
    private readonly IIntegrityPolicyService _policy;
    private readonly AuditService _audit;
    private readonly ILogger<IntegrityAssignmentService> _logger;

    public IntegrityAssignmentService(
        PayrollDbContext context,
        IIntegrityPolicyService policy,
        AuditService audit,
        ILogger<IntegrityAssignmentService> logger)
    {
        _context = context;
        _policy = policy;
        _audit = audit;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IntegrityLevel> GetUserIntegrityLevelAsync(int userId)
    {
        if (userId <= 0)
        {
            return IntegrityLevel.Untrusted;
        }

        // 1. Check explicit active database assignment first
        var explicitClassification = await _context.UserIntegrityClassifications
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        if (explicitClassification != null)
        {
            return explicitClassification.IntegrityLevel;
        }

        // 2. Fall back to role-based mapping with server-side account validation
        var user = await _context.UserAccounts
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null || !string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            // Inactive or missing accounts fail closed to Untrusted
            return IntegrityLevel.Untrusted;
        }

        var roles = user.UserRoleUsers
            .Select(ur => ur.Role?.RoleName)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            return IntegrityLevel.HighlyTrusted;
        }
        if (roles.Contains("HR Manager", StringComparer.OrdinalIgnoreCase) ||
            roles.Contains("Payroll Manager", StringComparer.OrdinalIgnoreCase))
        {
            return IntegrityLevel.Trusted;
        }
        if (roles.Contains("Employee", StringComparer.OrdinalIgnoreCase))
        {
            return IntegrityLevel.SlightlyTrusted;
        }

        return IntegrityLevel.Untrusted;
    }

    /// <inheritdoc />
    public IntegrityLevel GetUserIntegrityLevel(ClaimsPrincipal principal)
    {
        if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
        {
            return IntegrityLevel.Untrusted;
        }

        // Check if an explicit claim was set during authentication
        var claim = principal.FindFirst("IntegrityLevel")?.Value;
        if (!string.IsNullOrWhiteSpace(claim) && Enum.TryParse<IntegrityLevel>(claim, out var parsed))
        {
            return parsed;
        }

        // Evaluate from authenticated roles
        if (principal.IsInRole("Admin"))
        {
            return IntegrityLevel.HighlyTrusted;
        }
        if (principal.IsInRole("HR Manager") || principal.IsInRole("Payroll Manager"))
        {
            return IntegrityLevel.Trusted;
        }
        if (principal.IsInRole("Employee"))
        {
            return IntegrityLevel.SlightlyTrusted;
        }

        return IntegrityLevel.Untrusted;
    }

    /// <inheritdoc />
    public async Task<IntegrityLevel> GetObjectIntegrityLevelAsync(string entityType, int entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
        {
            return IntegrityLevel.Untrusted;
        }

        // 1. Check explicit active object classification
        var explicitClassification = await _context.ObjectIntegrityClassifications
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.EntityType == entityType && o.EntityId == entityId && o.IsActive);

        if (explicitClassification != null)
        {
            return explicitClassification.IntegrityLevel;
        }

        // 2. Dynamic baseline classification based on business status
        switch (entityType.ToLowerInvariant())
        {
            case "salarystructure":
                var salary = await _context.SalaryStructures.AsNoTracking().FirstOrDefaultAsync(s => s.SalaryStructureId == entityId);
                if (salary != null && string.Equals(salary.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    return IntegrityLevel.HighlyTrusted;
                }
                return salary != null ? IntegrityLevel.SlightlyTrusted : IntegrityLevel.Untrusted;

            case "payroll":
                var payroll = await _context.Payrolls.AsNoTracking().FirstOrDefaultAsync(p => p.PayrollId == entityId);
                if (payroll != null)
                {
                    if (string.Equals(payroll.PayrollStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(payroll.PayrollStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(payroll.PayrollStatus, "Finalized", StringComparison.OrdinalIgnoreCase))
                    {
                        return IntegrityLevel.HighlyTrusted;
                    }
                    if (string.Equals(payroll.PayrollStatus, "Processed", StringComparison.OrdinalIgnoreCase))
                    {
                        return IntegrityLevel.Trusted;
                    }
                    return IntegrityLevel.SlightlyTrusted;
                }
                return IntegrityLevel.Untrusted;

            case "bankaccount":
                var bank = await _context.BankAccounts.AsNoTracking().FirstOrDefaultAsync(b => b.BankAccountId == entityId);
                if (bank != null && bank.IsPrimary)
                {
                    return IntegrityLevel.HighlyTrusted;
                }
                return bank != null ? IntegrityLevel.SlightlyTrusted : IntegrityLevel.Untrusted;

            case "payment":
                var payment = await _context.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.PaymentId == entityId);
                if (payment != null && (string.Equals(payment.PaymentStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(payment.PaymentStatus, "Approved", StringComparison.OrdinalIgnoreCase)))
                {
                    return IntegrityLevel.HighlyTrusted;
                }
                return payment != null ? IntegrityLevel.Trusted : IntegrityLevel.Untrusted;

            case "employee":
                var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == entityId);
                if (emp != null && string.Equals(emp.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    return IntegrityLevel.Trusted;
                }
                return emp != null ? IntegrityLevel.SlightlyTrusted : IntegrityLevel.Untrusted;

            case "department":
                var dept = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == entityId);
                if (dept != null && string.Equals(dept.DepartmentStatus, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    return IntegrityLevel.Trusted;
                }
                return IntegrityLevel.Untrusted;

            case "attendance":
                var att = await _context.Attendances.AsNoTracking().FirstOrDefaultAsync(a => a.AttendanceId == entityId);
                if (att != null)
                {
                    return IntegrityLevel.Trusted;
                }
                return IntegrityLevel.Untrusted;

            case "leave":
                var leave = await _context.Leaves.AsNoTracking().FirstOrDefaultAsync(l => l.LeaveId == entityId);
                if (leave != null)
                {
                    if (string.Equals(leave.LeaveStatus, "Approved", StringComparison.OrdinalIgnoreCase))
                    {
                        return IntegrityLevel.Trusted;
                    }
                    if (string.Equals(leave.LeaveStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        return IntegrityLevel.SlightlyTrusted;
                    }
                    return IntegrityLevel.Untrusted;
                }
                return IntegrityLevel.Untrusted;

            default:
                return IntegrityLevel.Untrusted;
        }
    }

    /// <inheritdoc />
    public async Task<(bool Allowed, string Reason, IntegrityLevel SubjectLevel, IntegrityLevel ObjectLevel)> CheckReadAccessAsync(
        int userId,
        string entityType,
        int entityId,
        string? ipAddress)
    {
        var subjectLevel = await GetUserIntegrityLevelAsync(userId);
        var objectLevel = await GetObjectIntegrityLevelAsync(entityType, entityId);

        var eval = _policy.EvaluateRead(subjectLevel, objectLevel);
        string decision = eval.IsAllowed ? "ALLOWED" : "DENIED";

        await RecordAuditAsync(
            userId,
            eval.IsAllowed ? "BIBA_READ_ALLOWED" : "BIBA_READ_DENIED",
            entityType,
            entityId,
            subjectLevel,
            objectLevel,
            decision,
            eval.Reason,
            ipAddress);

        return (eval.IsAllowed, eval.Reason ?? string.Empty, subjectLevel, objectLevel);
    }

    /// <inheritdoc />
    public async Task<(bool Allowed, string Reason, IntegrityLevel SubjectLevel, IntegrityLevel ObjectLevel)> CheckWriteAccessAsync(
        int userId,
        string entityType,
        int entityId,
        string? ipAddress)
    {
        var subjectLevel = await GetUserIntegrityLevelAsync(userId);
        var objectLevel = await GetObjectIntegrityLevelAsync(entityType, entityId);

        var eval = _policy.EvaluateWrite(subjectLevel, objectLevel);
        string decision = eval.IsAllowed ? "ALLOWED" : "DENIED";

        await RecordAuditAsync(
            userId,
            eval.IsAllowed ? "BIBA_WRITE_ALLOWED" : "BIBA_WRITE_DENIED",
            entityType,
            entityId,
            subjectLevel,
            objectLevel,
            decision,
            eval.Reason,
            ipAddress);

        return (eval.IsAllowed, eval.Reason ?? string.Empty, subjectLevel, objectLevel);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Error)> AssignUserIntegrityAsync(
        int targetUserId,
        IntegrityLevel newLevel,
        int assignedByUserId,
        string reason)
    {
        try
        {
            if (targetUserId <= 0) return (false, "Target User ID must be positive.");
            if (assignedByUserId <= 0) return (false, "Assigned By User ID must be positive.");

            // Self-promotion prevention: A user cannot change their own integrity classification
            if (targetUserId == assignedByUserId)
            {
                return (false, "Self-promotion violation: You cannot alter your own integrity classification.");
            }

            var assignerLevel = await GetUserIntegrityLevelAsync(assignedByUserId);
            if (assignerLevel < IntegrityLevel.HighlyTrusted)
            {
                return (false, "Only HighlyTrusted administrators can assign or alter user integrity classifications.");
            }

            var previousLevel = await GetUserIntegrityLevelAsync(targetUserId);

            // Inactivate existing classification
            var existing = await _context.UserIntegrityClassifications
                .Where(u => u.UserId == targetUserId && u.IsActive)
                .ToListAsync();

            foreach (var item in existing)
            {
                item.IsActive = false;
            }

            var newClassification = new UserIntegrityClassification
            {
                UserId = targetUserId,
                IntegrityLevel = newLevel,
                AssignedBy = assignedByUserId,
                Reason = reason,
                IsActive = true,
                AssignedAtUtc = DateTime.UtcNow
            };

            _context.UserIntegrityClassifications.Add(newClassification);

            var history = new IntegrityClassificationHistory
            {
                EntityType = "UserAccount",
                EntityId = targetUserId,
                PreviousIntegrityLevel = previousLevel,
                NewIntegrityLevel = newLevel,
                ChangedBy = assignedByUserId,
                ChangedAtUtc = DateTime.UtcNow,
                Reason = reason,
                ApprovalReference = $"ASSIGN-USER-{targetUserId}"
            };

            _context.IntegrityClassificationHistories.Add(history);
            await _context.SaveChangesAsync();

            await RecordAuditAsync(
                assignedByUserId,
                "INTEGRITY_LEVEL_ASSIGNED",
                "UserAccount",
                targetUserId,
                assignerLevel,
                newLevel,
                "ALLOWED",
                $"Assigned User #{targetUserId} integrity from {previousLevel} to {newLevel}. Reason: {reason}",
                null);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign integrity level to User #{UserId}", targetUserId);
            return (false, "Failed to assign integrity level: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Error)> PromoteObjectIntegrityAsync(
        string entityType,
        int entityId,
        IntegrityLevel newLevel,
        int approverUserId,
        string reason,
        string? approvalRef = null)
    {
        try
        {
            var approverLevel = await GetUserIntegrityLevelAsync(approverUserId);
            var previousLevel = await GetObjectIntegrityLevelAsync(entityType, entityId);

            // Validation: Promotion requires strictly higher new level
            if (newLevel <= previousLevel)
            {
                return (false, $"Promotion requires New Level ({newLevel}) to be strictly higher than Current Level ({previousLevel}).");
            }

            // Approver must be at least as trusted as the target level (No Write Up)
            if (approverLevel < newLevel)
            {
                return (false, $"Approver integrity level ({approverLevel}) is insufficient to promote object to {newLevel}.");
            }

            // Inactivate existing classification
            var existing = await _context.ObjectIntegrityClassifications
                .Where(o => o.EntityType == entityType && o.EntityId == entityId && o.IsActive)
                .ToListAsync();

            foreach (var item in existing)
            {
                item.IsActive = false;
            }

            var newClassification = new ObjectIntegrityClassification
            {
                EntityType = entityType,
                EntityId = entityId,
                IntegrityLevel = newLevel,
                AssignedBy = approverUserId,
                Reason = reason,
                IsActive = true,
                AssignedAtUtc = DateTime.UtcNow
            };

            _context.ObjectIntegrityClassifications.Add(newClassification);

            var history = new IntegrityClassificationHistory
            {
                EntityType = entityType,
                EntityId = entityId,
                PreviousIntegrityLevel = previousLevel,
                NewIntegrityLevel = newLevel,
                ChangedBy = approverUserId,
                ChangedAtUtc = DateTime.UtcNow,
                Reason = reason,
                ApprovalReference = approvalRef ?? $"PROMO-{entityType}-{entityId}"
            };

            _context.IntegrityClassificationHistories.Add(history);
            await _context.SaveChangesAsync();

            await RecordAuditAsync(
                approverUserId,
                "INTEGRITY_PROMOTION_APPROVED",
                entityType,
                entityId,
                approverLevel,
                newLevel,
                "ALLOWED",
                $"Promoted {entityType} #{entityId} from {previousLevel} to {newLevel}. Reason: {reason}",
                null);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to promote integrity level for {EntityType} #{EntityId}", entityType, entityId);
            return (false, "Failed to promote integrity level: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Error)> DemoteObjectIntegrityAsync(
        string entityType,
        int entityId,
        IntegrityLevel newLevel,
        int approverUserId,
        string reason)
    {
        try
        {
            var approverLevel = await GetUserIntegrityLevelAsync(approverUserId);
            var previousLevel = await GetObjectIntegrityLevelAsync(entityType, entityId);

            if (newLevel >= previousLevel)
            {
                return (false, $"Demotion requires New Level ({newLevel}) to be strictly lower than Current Level ({previousLevel}).");
            }

            if (approverLevel < IntegrityLevel.Trusted)
            {
                return (false, $"Approver integrity level ({approverLevel}) is insufficient to demote object.");
            }

            var existing = await _context.ObjectIntegrityClassifications
                .Where(o => o.EntityType == entityType && o.EntityId == entityId && o.IsActive)
                .ToListAsync();

            foreach (var item in existing)
            {
                item.IsActive = false;
            }

            var newClassification = new ObjectIntegrityClassification
            {
                EntityType = entityType,
                EntityId = entityId,
                IntegrityLevel = newLevel,
                AssignedBy = approverUserId,
                Reason = reason,
                IsActive = true,
                AssignedAtUtc = DateTime.UtcNow
            };

            _context.ObjectIntegrityClassifications.Add(newClassification);

            var history = new IntegrityClassificationHistory
            {
                EntityType = entityType,
                EntityId = entityId,
                PreviousIntegrityLevel = previousLevel,
                NewIntegrityLevel = newLevel,
                ChangedBy = approverUserId,
                ChangedAtUtc = DateTime.UtcNow,
                Reason = reason,
                ApprovalReference = $"DEMOTE-{entityType}-{entityId}"
            };

            _context.IntegrityClassificationHistories.Add(history);
            await _context.SaveChangesAsync();

            await RecordAuditAsync(
                approverUserId,
                "INTEGRITY_LEVEL_CHANGED",
                entityType,
                entityId,
                approverLevel,
                newLevel,
                "ALLOWED",
                $"Demoted {entityType} #{entityId} from {previousLevel} to {newLevel}. Reason: {reason}",
                null);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to demote integrity level for {EntityType} #{EntityId}", entityType, entityId);
            return (false, "Failed to demote integrity level: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task RecordAuditAsync(
        int userId,
        string operation,
        string entityType,
        int entityId,
        IntegrityLevel subjectLevel,
        IntegrityLevel objectLevel,
        string decision,
        string? reason,
        string? ipAddress)
    {
        try
        {
            if (userId <= 0)
            {
                // Unauthenticated or unknown identity fails closed and does not violate FK
                var adminUser = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.Username == "aarav.mehta");
                if (adminUser != null)
                {
                    userId = adminUser.UserId;
                }
            }

            var auditEntry = new IntegritySecurityAudit
            {
                UserId = userId,
                Operation = operation.Length > 30 ? operation[..30] : operation,
                EntityType = entityType.Length > 50 ? entityType[..50] : entityType,
                EntityId = entityId,
                SubjectIntegrityLevel = subjectLevel,
                ObjectIntegrityLevel = objectLevel,
                Decision = decision.Length > 20 ? decision[..20] : decision,
                Reason = reason != null && reason.Length > 500 ? reason[..500] : reason,
                IpAddress = ipAddress != null && ipAddress.Length > 45 ? ipAddress[..45] : ipAddress,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.IntegritySecurityAudits.Add(auditEntry);
            await _context.SaveChangesAsync();

            // Also mirror to existing AUDIT_LOG table for full platform unified audit trail
            await _audit.LogAsync(
                userId,
                operation,
                entityType,
                entityId,
                string.Equals(decision, "ALLOWED", StringComparison.OrdinalIgnoreCase) ? "Success" : "Failure",
                reason,
                ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write Biba integrity security audit for Operation {Operation}", operation);
        }
    }
}
