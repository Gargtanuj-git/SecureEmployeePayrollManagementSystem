using System;
using System.Collections.Generic;

namespace SecureEmployeePayrollSystem.Models;

public partial class Attendance
{
    public int AttendanceId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    public string AttendanceStatus { get; set; } = null!;

    public decimal? WorkingHours { get; set; }

    public decimal? OvertimeHours { get; set; }

    public int EmployeeId { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
