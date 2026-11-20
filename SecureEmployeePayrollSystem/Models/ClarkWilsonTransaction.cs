using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class ClarkWilsonTransaction
{
    public long TransactionId { get; set; }

    public string TransactionType { get; set; } = null!;

    public string TargetEntityType { get; set; } = null!;

    public int TargetEntityId { get; set; }

    public string? PayloadJson { get; set; }

    public string Status { get; set; } = "PENDING";

    public int SubmittedByUserId { get; set; }

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public string? Justification { get; set; }

    public byte[] ConcurrencyToken { get; set; } = null!;

    public virtual UserAccount SubmittedByUser { get; set; } = null!;

    public virtual ICollection<ClarkWilsonApproval> Approvals { get; set; } = new List<ClarkWilsonApproval>();
}
