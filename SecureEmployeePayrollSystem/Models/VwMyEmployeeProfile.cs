using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class VwMyEmployeeProfile
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public int EmployeeId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Address { get; set; } = null!;

    public DateOnly HireDate { get; set; }

    public string EmploymentStatus { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;

    public string DesignationName { get; set; } = null!;
}
