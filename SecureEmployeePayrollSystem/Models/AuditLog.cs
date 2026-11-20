using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class AuditLog
{
    public long AuditId { get; set; }

    public string TableName { get; set; } = null!;

    public int RecordId { get; set; }

    public string ActionType { get; set; } = null!;

    public DateTime ActionDateTime { get; set; }

    public string? IpAddress { get; set; }

    public string ActionStatus { get; set; } = null!;

    public string? Description { get; set; }

    public int UserId { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}
