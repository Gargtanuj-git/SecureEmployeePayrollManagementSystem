using System.Security.Claims;
using System.Threading.Tasks;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public interface IIntegrityAssignmentService
{
    /// <summary>
    /// Retrieves the server-side integrity level for a user by User ID.
    /// Fails closed to Untrusted if user is unknown or unverified.
    /// </summary>
    Task<IntegrityLevel> GetUserIntegrityLevelAsync(int userId);

    /// <summary>
    /// Retrieves the effective integrity level from a user's ClaimsPrincipal.
    /// </summary>
    IntegrityLevel GetUserIntegrityLevel(ClaimsPrincipal principal);

    /// <summary>
    /// Retrieves the server-side integrity level for an object by Entity Type and Entity ID.
    /// Fails closed if classification cannot be determined for protected resources.
    /// </summary>
    Task<IntegrityLevel> GetObjectIntegrityLevelAsync(string entityType, int entityId);

    /// <summary>
    /// Evaluates Biba Read access (No Read Down), logs the decision to the integrity audit log,
    /// and returns whether the operation is permitted.
    /// </summary>
    Task<(bool Allowed, string Reason, IntegrityLevel SubjectLevel, IntegrityLevel ObjectLevel)> CheckReadAccessAsync(
        int userId,
        string entityType,
        int entityId,
        string? ipAddress);

    /// <summary>
    /// Evaluates Biba Write access (No Write Up), logs the decision to the integrity audit log,
    /// and returns whether the operation is permitted.
    /// </summary>
    Task<(bool Allowed, string Reason, IntegrityLevel SubjectLevel, IntegrityLevel ObjectLevel)> CheckWriteAccessAsync(
        int userId,
        string entityType,
        int entityId,
        string? ipAddress);

    /// <summary>
    /// Assigns or updates a user's explicit integrity classification with audit history.
    /// </summary>
    Task<(bool Success, string Error)> AssignUserIntegrityAsync(
        int targetUserId,
        IntegrityLevel newLevel,
        int assignedByUserId,
        string reason);

    /// <summary>
    /// Formally promotes an object's integrity level through an authorized approval workflow.
    /// </summary>
    Task<(bool Success, string Error)> PromoteObjectIntegrityAsync(
        string entityType,
        int entityId,
        IntegrityLevel newLevel,
        int approverUserId,
        string reason,
        string? approvalRef = null);

    /// <summary>
    /// Formally demotes an object's integrity level.
    /// </summary>
    Task<(bool Success, string Error)> DemoteObjectIntegrityAsync(
        string entityType,
        int entityId,
        IntegrityLevel newLevel,
        int approverUserId,
        string reason);

    /// <summary>
    /// Persists an integrity audit event.
    /// </summary>
    Task RecordAuditAsync(
        int userId,
        string operation,
        string entityType,
        int entityId,
        IntegrityLevel subjectLevel,
        IntegrityLevel objectLevel,
        string decision,
        string? reason,
        string? ipAddress);
}
