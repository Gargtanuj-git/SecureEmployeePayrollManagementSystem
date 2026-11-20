using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class VwPayrollSummary
{
    public int PayrollId { get; set; }

    public int EmployeeId { get; set; }

    public string EmployeeName { get; set; } = null!;

    public DateOnly PayPeriodStart { get; set; }

    public DateOnly PayPeriodEnd { get; set; }

    public decimal GrossSalary { get; set; }

    public decimal TotalDeductions { get; set; }

    public decimal? NetSalary { get; set; }

    public string PayrollStatus { get; set; } = null!;

    public DateOnly GeneratedDate { get; set; }
}
