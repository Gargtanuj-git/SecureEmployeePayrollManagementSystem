using System;

namespace SecureEmployeePayrollSystem.Models;

public partial class UserIntegrityClassification
{
    public int UserIntegrityId { get; set; }

    public int UserId { get; set; }

    public IntegrityLevel IntegrityLevel { get; set; }

    public int AssignedBy { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    public string? Reason { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual UserAccount User { get; set; } = null!;

    public virtual UserAccount AssigningUser { get; set; } = null!;
}
