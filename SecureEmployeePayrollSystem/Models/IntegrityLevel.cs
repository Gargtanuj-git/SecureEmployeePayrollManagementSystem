namespace SecureEmployeePayrollSystem.Models;

/// <summary>
/// Classical Biba Integrity Model 4-Tier Linear Integrity Scale.
/// Higher numeric value denotes higher integrity / trustworthiness.
/// </summary>
public enum IntegrityLevel
{
    /// <summary>
    /// Level 1: Untrusted external inputs, unverified self-registrations, unvetted data.
    /// </summary>
    Untrusted = 1,

    /// <summary>
    /// Level 2: Standard employee self-service input, pending requests, draft updates.
    /// </summary>
    SlightlyTrusted = 2,

    /// <summary>
    /// Level 3: Verified corporate records, HR/Payroll manager operational data, verified profiles.
    /// </summary>
    Trusted = 3,

    /// <summary>
    /// Level 4: Finalized payroll, approved salary structures, verified primary bank accounts, executive audit.
    /// </summary>
    HighlyTrusted = 4
}
