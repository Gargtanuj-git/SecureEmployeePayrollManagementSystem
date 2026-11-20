using System;

namespace SecureEmployeePayrollSystem.Models;

public partial class ClarkWilsonAudit
{
    public long AuditId { get; set; }

    public int UserId { get; set; }

    public string EventType { get; set; } = null!;

    public string TargetEntity { get; set; } = null!;

    public int? TargetEntityId { get; set; }

    public string Outcome { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? IpAddress { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}
