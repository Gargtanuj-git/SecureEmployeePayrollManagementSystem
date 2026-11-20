using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class Leave
{
    public int LeaveId { get; set; }

    public string LeaveType { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal NumberOfDays { get; set; }

    public string LeaveStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public DateOnly? ApprovedDate { get; set; }

    public int EmployeeId { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
