using System;

namespace SecureEmployeePayrollSystem.Models;

public partial class ClarkWilsonVerificationLog
{
    public long LogId { get; set; }

    public string IvpName { get; set; } = null!;

    public string TargetEntity { get; set; } = null!;

    public int TargetEntityId { get; set; }

    public bool IsConsistent { get; set; }

    public string? ValidationErrors { get; set; }

    public DateTime VerifiedAtUtc { get; set; } = DateTime.UtcNow;
}
