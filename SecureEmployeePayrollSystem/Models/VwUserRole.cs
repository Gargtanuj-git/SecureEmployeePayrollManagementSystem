using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class VwUserRole
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public int EmployeeId { get; set; }

    public string EmployeeName { get; set; } = null!;

    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public string? RoleDescription { get; set; }

    public DateTime AssignedAt { get; set; }
}
