using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class VwAuditReport
{
    public long AuditId { get; set; }

    public string TableName { get; set; } = null!;

    public int RecordId { get; set; }

    public string ActionType { get; set; } = null!;

    public DateTime ActionDateTime { get; set; }

    public string Username { get; set; } = null!;

    public string? IpAddress { get; set; }

    public string ActionStatus { get; set; } = null!;

    public string? Description { get; set; }
}
