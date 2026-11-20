using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public DateOnly PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? TransactionReference { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public int PayrollItemId { get; set; }

    public virtual PayrollItem PayrollItem { get; set; } = null!;
}
