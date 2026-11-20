using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class UserAccount
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string AccountStatus { get; set; } = null!;

    public DateTime? LastLogin { get; set; }

    public DateTime CreatedAt { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public int EmployeeId { get; set; }

    public bool MfaEnabled { get; set; }

    public string? MfaType { get; set; }

    public string? MfaSecretKey { get; set; }

    public string? MfaRecoveryCodes { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? EmailOtpCode { get; set; }

    public DateTime? EmailOtpExpiry { get; set; }

    public int EmailOtpAttempts { get; set; }

    public string? EmailVerificationTokenHash { get; set; }

    public DateTime? EmailVerificationTokenExpiry { get; set; }

    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual Employee Employee { get; set; } = null!;

    public virtual ICollection<UserRole> UserRoleAssignedByNavigations { get; set; } = new List<UserRole>();

    public virtual ICollection<UserRole> UserRoleUsers { get; set; } = new List<UserRole>();
}
