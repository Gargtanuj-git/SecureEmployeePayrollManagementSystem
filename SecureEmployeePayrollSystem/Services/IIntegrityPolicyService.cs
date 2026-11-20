using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public record IntegrityEvaluationResult(
    bool IsAllowed,
    IntegrityLevel SubjectLevel,
    IntegrityLevel ObjectLevel,
    string Operation,
    string? Reason);

/// <summary>
/// Classical Biba Integrity Model Decision Service.
/// Single source of truth for Biba security decisions across SEPS.
/// </summary>
public interface IIntegrityPolicyService
{
    /// <summary>
    /// Rule A (No Read Down): A subject must not read an object with a lower integrity level.
    /// Allowed if and only if: SubjectIntegrityLevel &lt;= ObjectIntegrityLevel
    /// </summary>
    bool CanRead(IntegrityLevel subjectLevel, IntegrityLevel objectLevel);

    /// <summary>
    /// Rule B (No Write Up): A subject must not write to an object with a higher integrity level.
    /// Allowed if and only if: SubjectIntegrityLevel &gt;= ObjectIntegrityLevel
    /// </summary>
    bool CanWrite(IntegrityLevel subjectLevel, IntegrityLevel objectLevel);

    /// <summary>
    /// Evaluates Read access with formal audit reasoning.
    /// </summary>
    IntegrityEvaluationResult EvaluateRead(IntegrityLevel subjectLevel, IntegrityLevel objectLevel);

    /// <summary>
    /// Evaluates Write access with formal audit reasoning.
    /// </summary>
    IntegrityEvaluationResult EvaluateWrite(IntegrityLevel subjectLevel, IntegrityLevel objectLevel);
}
