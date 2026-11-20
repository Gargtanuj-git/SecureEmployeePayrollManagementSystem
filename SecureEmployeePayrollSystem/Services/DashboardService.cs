using System;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class DashboardViewModel
{
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public int DepartmentCount { get; set; }
    public int PendingLeaves { get; set; }
    public int TodayPresent { get; set; }
    public int TodayAbsent { get; set; }
    public int TodayOnLeave { get; set; }
    public decimal LatestPayrollExpenditure { get; set; }
    public int TotalPayslipsGenerated { get; set; }

    // Additional Summary Metrics matching reference design
    public int TotalLeaveRequests { get; set; }
    public int ApprovedLeaves { get; set; }
    public int RejectedLeaves { get; set; }
    public decimal LatestPayrollProcessedAmount { get; set; }
    public decimal LatestPayrollPendingAmount { get; set; }
    public int EmployeesPaidCount { get; set; }
    public string LatestPayrollMonthLabel { get; set; } = string.Empty;
    public List<DailyAttendancePoint> AttendanceLast7Days { get; set; } = new();

    // Chart Data
    public Dictionary<string, int> EmployeesByDepartment { get; set; } = new();
    public Dictionary<string, int> AttendanceStatusBreakdown { get; set; } = new();
    public List<MonthlyPayrollTrend> MonthlyPayrollTrends { get; set; } = new();

    // Recent Activity
    public List<AuditLog> RecentActivities { get; set; } = new();

    // Employee-specific fields (for Employee role view)
    public Employee? MyProfile { get; set; }
    public List<Attendance> MyRecentAttendance { get; set; } = new();
    public List<Leave> MyRecentLeaves { get; set; } = new();
    public List<Payroll> MyRecentPayslips { get; set; } = new();
}

public class DailyAttendancePoint
{
    public string DayLabel { get; set; } = string.Empty;
    public int PresentCount { get; set; }
}

public class MonthlyPayrollTrend
{
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal GrossSalary { get; set; }
    public decimal NetSalary { get; set; }
}

public class DashboardService
{
    private readonly PayrollDbContext _context;

    public DashboardService(PayrollDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync(int? currentEmployeeId, bool isAdminOrManager)
    {
        var model = new DashboardViewModel();

        model.TotalEmployees = await _context.Employees.CountAsync();
        model.ActiveEmployees = await _context.Employees.CountAsync(e => e.EmploymentStatus == "Active");
        model.DepartmentCount = await _context.Departments.CountAsync(d => d.DepartmentStatus == "Active");
        model.PendingLeaves = await _context.Leaves.CountAsync(l => l.LeaveStatus == "Pending");

        model.TotalLeaveRequests = await _context.Leaves.CountAsync();
        model.ApprovedLeaves = await _context.Leaves.CountAsync(l => l.LeaveStatus == "Approved");
        model.RejectedLeaves = await _context.Leaves.CountAsync(l => l.LeaveStatus == "Rejected");

        var today = DateOnly.FromDateTime(DateTime.Now);
        var todayAttendances = await _context.Attendances
            .Where(a => a.AttendanceDate == today)
            .ToListAsync();

        model.TodayPresent = todayAttendances.Count(a => a.AttendanceStatus == "Present");
        model.TodayAbsent = todayAttendances.Count(a => a.AttendanceStatus == "Absent");
        model.TodayOnLeave = todayAttendances.Count(a => a.AttendanceStatus == "Leave");

        // 7-day attendance trend
        for (int i = 6; i >= 0; i--)
        {
            var dayDate = today.AddDays(-i);
            var presentOnDay = await _context.Attendances
                .CountAsync(a => a.AttendanceDate == dayDate && a.AttendanceStatus == "Present");

            model.AttendanceLast7Days.Add(new DailyAttendancePoint
            {
                DayLabel = dayDate.ToString("dd MMM"),
                PresentCount = presentOnDay
            });
        }

        var latestPayroll = await _context.Payrolls
            .OrderByDescending(p => p.PayPeriodEnd)
            .Take(1)
            .FirstOrDefaultAsync();

        if (latestPayroll != null)
        {
            model.LatestPayrollMonthLabel = latestPayroll.PayPeriodStart.ToString("MMMM yyyy");
            var periodPayrolls = await _context.Payrolls
                .Where(p => p.PayPeriodStart == latestPayroll.PayPeriodStart && p.PayPeriodEnd == latestPayroll.PayPeriodEnd)
                .ToListAsync();

            model.LatestPayrollExpenditure = periodPayrolls.Sum(p => p.GrossSalary);
            model.LatestPayrollProcessedAmount = periodPayrolls
                .Where(p => p.PayrollStatus == "Paid" || p.PayrollStatus == "Processed")
                .Sum(p => p.GrossSalary);
            model.LatestPayrollPendingAmount = periodPayrolls
                .Where(p => p.PayrollStatus != "Paid" && p.PayrollStatus != "Processed")
                .Sum(p => p.GrossSalary);
            model.EmployeesPaidCount = periodPayrolls
                .Count(p => p.PayrollStatus == "Paid" || p.PayrollStatus == "Processed");
        }
        else
        {
            model.LatestPayrollMonthLabel = DateTime.Today.ToString("MMMM yyyy");
        }

        model.TotalPayslipsGenerated = await _context.Payrolls.CountAsync();

        // Department breakdown
        var deptGroups = await _context.Departments
            .Include(d => d.Employees)
            .Select(d => new { d.DepartmentName, Count = d.Employees.Count })
            .ToListAsync();

        foreach (var dg in deptGroups)
        {
            model.EmployeesByDepartment[dg.DepartmentName] = dg.Count;
        }

        // Attendance status
        model.AttendanceStatusBreakdown["Present"] = model.TodayPresent;
        model.AttendanceStatusBreakdown["Absent"] = model.TodayAbsent;
        model.AttendanceStatusBreakdown["Leave"] = model.TodayOnLeave;
        model.AttendanceStatusBreakdown["Half Day"] = todayAttendances.Count(a => a.AttendanceStatus == "Half Day");

        // Payroll trends
        var rawPeriods = await _context.Payrolls
            .GroupBy(p => new { p.PayPeriodStart, p.PayPeriodEnd })
            .Select(g => new 
            {
                g.Key.PayPeriodStart,
                g.Key.PayPeriodEnd,
                GrossSalary = g.Sum(p => p.GrossSalary),
                NetSalary = g.Sum(p => p.NetSalary ?? (p.GrossSalary - p.TotalDeductions))
            })
            .OrderByDescending(t => t.PayPeriodEnd)
            .Take(6)
            .ToListAsync();

        model.MonthlyPayrollTrends = rawPeriods.Select(t => new MonthlyPayrollTrend
        {
            PeriodLabel = t.PayPeriodStart.ToString("MMM yyyy"),
            GrossSalary = t.GrossSalary,
            NetSalary = t.NetSalary
        }).ToList();

        // Recent Audit logs
        model.RecentActivities = await _context.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.ActionDateTime)
            .Take(8)
            .ToListAsync();

        // If Employee role: load personal records
        if (currentEmployeeId.HasValue && currentEmployeeId.Value > 0)
        {
            int empId = currentEmployeeId.Value;
            model.MyProfile = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Include(e => e.SalaryStructure)
                .FirstOrDefaultAsync(e => e.EmployeeId == empId);

            model.MyRecentAttendance = await _context.Attendances
                .Where(a => a.EmployeeId == empId)
                .OrderByDescending(a => a.AttendanceDate)
                .Take(7)
                .ToListAsync();

            model.MyRecentLeaves = await _context.Leaves
                .Where(l => l.EmployeeId == empId)
                .OrderByDescending(l => l.StartDate)
                .Take(5)
                .ToListAsync();

            model.MyRecentPayslips = await _context.Payrolls
                .Where(p => p.EmployeeId == empId)
                .OrderByDescending(p => p.PayPeriodEnd)
                .Take(4)
                .ToListAsync();
        }

        return model;
    }
}
