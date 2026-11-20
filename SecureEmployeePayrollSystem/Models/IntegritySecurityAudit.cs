using System;

namespace SecureEmployeePayrollSystem.Models;

public partial class IntegritySecurityAudit
{
    public long AuditId { get; set; }

    public int UserId { get; set; }

    public string Operation { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public IntegrityLevel SubjectIntegrityLevel { get; set; }

    public IntegrityLevel ObjectIntegrityLevel { get; set; }

    public string Decision { get; set; } = null!;

    public string? Reason { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual UserAccount User { get; set; } = null!;
}
