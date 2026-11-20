using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class SalaryComponent
{
    public int ComponentId { get; set; }

    public string ComponentName { get; set; } = null!;

    public string ComponentType { get; set; } = null!;

    public string CalculationType { get; set; } = null!;

    public decimal? Percentage { get; set; }

    public decimal? FixedAmount { get; set; }

    public bool TaxableFlag { get; set; }

    public int SalaryStructureId { get; set; }

    public virtual SalaryStructure SalaryStructure { get; set; } = null!;
}
