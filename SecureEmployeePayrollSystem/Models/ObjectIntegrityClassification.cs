using System;

namespace SecureEmployeePayrollSystem.Models;

public partial class ObjectIntegrityClassification
{
    public int ObjectIntegrityId { get; set; }

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public IntegrityLevel IntegrityLevel { get; set; }

    public int AssignedBy { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    public string? Reason { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual UserAccount AssigningUser { get; set; } = null!;
}
