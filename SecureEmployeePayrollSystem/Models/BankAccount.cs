using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class BankAccount
{
    public int BankAccountId { get; set; }

    public string RoleName { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    public string AccountType { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public int EmployeeId { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
