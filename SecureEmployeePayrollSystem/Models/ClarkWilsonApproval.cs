using System;

namespace SecureEmployeePayrollSystem.Models;

public partial class ClarkWilsonApproval
{
    public long ApprovalId { get; set; }

    public long TransactionId { get; set; }

    public int ApproverUserId { get; set; }

    public string Decision { get; set; } = null!;

    public DateTime DecisionAtUtc { get; set; } = DateTime.UtcNow;

    public string? Remarks { get; set; }

    public virtual ClarkWilsonTransaction Transaction { get; set; } = null!;

    public virtual UserAccount ApproverUser { get; set; } = null!;
}
