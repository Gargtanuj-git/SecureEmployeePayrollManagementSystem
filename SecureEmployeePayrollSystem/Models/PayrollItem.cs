using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class PayrollItem
{
    public int PayrollItemId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? CalculationBasis { get; set; }

    public string ItemType { get; set; } = null!;

    public int PayrollId { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Payroll Payroll { get; set; } = null!;
}
