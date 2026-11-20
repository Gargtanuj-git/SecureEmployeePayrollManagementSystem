using Microsoft.Extensions.Logging;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

/// <summary>
/// Centralized implementation of the classical Biba Integrity Model.
/// Enforces No Read Down (Subject &lt;= Object) and No Write Up (Subject &gt;= Object).
/// </summary>
public class IntegrityPolicyService : IIntegrityPolicyService
{
    private readonly ILogger<IntegrityPolicyService> _logger;

    public IntegrityPolicyService(ILogger<IntegrityPolicyService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanRead(IntegrityLevel subjectLevel, IntegrityLevel objectLevel)
    {
        // Rule A (No Read Down): Subject cannot read lower integrity objects
        return subjectLevel <= objectLevel;
    }

    /// <inheritdoc />
    public bool CanWrite(IntegrityLevel subjectLevel, IntegrityLevel objectLevel)
    {
        // Rule B (No Write Up): Subject cannot write to higher integrity objects
        return subjectLevel >= objectLevel;
    }

    /// <inheritdoc />
    public IntegrityEvaluationResult EvaluateRead(IntegrityLevel subjectLevel, IntegrityLevel objectLevel)
    {
        bool allowed = CanRead(subjectLevel, objectLevel);
        string reason = allowed
            ? $"Biba Read Allowed: Subject level {subjectLevel} ({((int)subjectLevel)}) <= Object level {objectLevel} ({((int)objectLevel)})."
            : $"Biba Read Denied (No Read Down): Subject level {subjectLevel} ({((int)subjectLevel)}) cannot read lower-integrity Object level {objectLevel} ({((int)objectLevel)}).";

        if (!allowed)
        {
            _logger.LogWarning("Biba Read Violation: Subject {SubjectLevel} attempted to read down to Object {ObjectLevel}.", subjectLevel, objectLevel);
        }

        return new IntegrityEvaluationResult(allowed, subjectLevel, objectLevel, "READ", reason);
    }

    /// <inheritdoc />
    public IntegrityEvaluationResult EvaluateWrite(IntegrityLevel subjectLevel, IntegrityLevel objectLevel)
    {
        bool allowed = CanWrite(subjectLevel, objectLevel);
        string reason = allowed
            ? $"Biba Write Allowed: Subject level {subjectLevel} ({((int)subjectLevel)}) >= Object level {objectLevel} ({((int)objectLevel)})."
            : $"Biba Write Denied (No Write Up): Subject level {subjectLevel} ({((int)subjectLevel)}) cannot write up to higher-integrity Object level {objectLevel} ({((int)objectLevel)}).";

        if (!allowed)
        {
            _logger.LogWarning("Biba Write Violation: Subject {SubjectLevel} attempted to write up to Object {ObjectLevel}.", subjectLevel, objectLevel);
        }

        return new IntegrityEvaluationResult(allowed, subjectLevel, objectLevel, "WRITE", reason);
    }
}
