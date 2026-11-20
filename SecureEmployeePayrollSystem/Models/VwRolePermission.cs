using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class VwRolePermission
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public int PermissionId { get; set; }

    public string PermissionName { get; set; } = null!;

    public string? PermissionDescription { get; set; }
}
