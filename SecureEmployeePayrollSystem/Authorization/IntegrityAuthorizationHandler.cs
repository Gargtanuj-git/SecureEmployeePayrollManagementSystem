using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Authorization;

/// <summary>
/// ASP.NET Core Authorization Handler that validates subject integrity level
/// against the Biba Integrity requirement.
/// </summary>
public class IntegrityAuthorizationHandler : AuthorizationHandler<IntegrityRequirement>
{
    private readonly IIntegrityPolicyService _policy;
    private readonly IIntegrityAssignmentService _assignmentService;
    private readonly ILogger<IntegrityAuthorizationHandler> _logger;

    public IntegrityAuthorizationHandler(
        IIntegrityPolicyService policy,
        IIntegrityAssignmentService assignmentService,
        ILogger<IntegrityAuthorizationHandler> logger)
    {
        _policy = policy;
        _assignmentService = assignmentService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IntegrityRequirement requirement)
    {
        if (context.User == null || context.User.Identity == null || !context.User.Identity.IsAuthenticated)
        {
            _logger.LogWarning("Biba authorization rejected: Unauthenticated subject.");
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int userId = int.TryParse(userIdClaim, out int id) ? id : 0;

        IntegrityLevel subjectLevel;
        if (userId > 0)
        {
            subjectLevel = await _assignmentService.GetUserIntegrityLevelAsync(userId);
        }
        else
        {
            subjectLevel = _assignmentService.GetUserIntegrityLevel(context.User);
        }

        bool allowed;
        if (requirement.Action == BibaAction.Write)
        {
            // Rule B (No Write Up): Subject must be >= Required Object Level
            allowed = _policy.CanWrite(subjectLevel, requirement.RequiredLevel);
        }
        else
        {
            // Rule A (No Read Down): Subject must be <= Required Object Level
            allowed = _policy.CanRead(subjectLevel, requirement.RequiredLevel);
        }

        if (allowed)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Biba authorization handler denied access: Subject {SubjectLevel} failed requirement for Action {Action} on Level {RequiredLevel} (Resource: {ResourceType}).",
                subjectLevel,
                requirement.Action,
                requirement.RequiredLevel,
                requirement.ResourceType ?? "Generic");
        }
    }
}
