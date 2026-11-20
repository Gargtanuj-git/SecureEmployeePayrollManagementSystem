using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public string Gender { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Address { get; set; } = null!;

    public DateOnly HireDate { get; set; }

    public string EmploymentStatus { get; set; } = null!;

    public int DepartmentId { get; set; }

    public int DesignationId { get; set; }

    public int SalaryStructureId { get; set; }

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual BankAccount? BankAccount { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual Designation Designation { get; set; } = null!;

    public virtual ICollection<Leave> Leaves { get; set; } = new List<Leave>();

    public virtual ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();

    public virtual SalaryStructure SalaryStructure { get; set; } = null!;

    public virtual UserAccount? UserAccount { get; set; }
}
