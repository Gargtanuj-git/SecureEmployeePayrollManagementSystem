using Microsoft.AspNetCore.Authorization;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Authorization;

public enum BibaAction
{
    Read,
    Write
}

/// <summary>
/// ASP.NET Core Authorization Requirement representing a Biba Integrity check.
/// </summary>
public class IntegrityRequirement : IAuthorizationRequirement
{
    public IntegrityLevel RequiredLevel { get; }
    public BibaAction Action { get; }
    public string? ResourceType { get; }

    public IntegrityRequirement(IntegrityLevel requiredLevel, BibaAction action = BibaAction.Write, string? resourceType = null)
    {
        RequiredLevel = requiredLevel;
        Action = action;
        ResourceType = resourceType;
    }
}
