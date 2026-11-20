using System;

namespace SecureEmployeePayrollSystem.Models;

public partial class IntegrityClassificationHistory
{
    public long HistoryId { get; set; }

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public IntegrityLevel PreviousIntegrityLevel { get; set; }

    public IntegrityLevel NewIntegrityLevel { get; set; }

    public int ChangedBy { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public string Reason { get; set; } = null!;

    public string? ApprovalReference { get; set; }

    public virtual UserAccount ChangingUser { get; set; } = null!;
}
