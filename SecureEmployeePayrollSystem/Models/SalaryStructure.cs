using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class SalaryStructure
{
    public int SalaryStructureId { get; set; }

    public string StructureName { get; set; } = null!;

    public decimal BasicSalary { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<SalaryComponent> SalaryComponents { get; set; } = new List<SalaryComponent>();
}
