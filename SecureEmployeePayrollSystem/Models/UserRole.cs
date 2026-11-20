using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class UserRole
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }

    public int? AssignedBy { get; set; }

    public virtual UserAccount? AssignedByNavigation { get; set; }

    public virtual AppRole Role { get; set; } = null!;

    public virtual UserAccount User { get; set; } = null!;
}
