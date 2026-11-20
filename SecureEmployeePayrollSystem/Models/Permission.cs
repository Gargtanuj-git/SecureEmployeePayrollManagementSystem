using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class Permission
{
    public int PermissionId { get; set; }

    public string PermissionName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<AppRole> Roles { get; set; } = new List<AppRole>();
}
