using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Data;

public partial class PayrollDbContext : DbContext
{
    public PayrollDbContext(DbContextOptions<PayrollDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AppRole> AppRoles { get; set; }

    public virtual DbSet<Attendance> Attendances { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BankAccount> BankAccounts { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Designation> Designations { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<Leave> Leaves { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Payroll> Payrolls { get; set; }

    public virtual DbSet<PayrollItem> PayrollItems { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<SalaryComponent> SalaryComponents { get; set; }

    public virtual DbSet<SalaryStructure> SalaryStructures { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<VwAuditReport> VwAuditReports { get; set; }

    public virtual DbSet<VwMyEmployeeProfile> VwMyEmployeeProfiles { get; set; }

    public virtual DbSet<VwPayrollSummary> VwPayrollSummaries { get; set; }

    public virtual DbSet<VwRolePermission> VwRolePermissions { get; set; }

    public virtual DbSet<VwUserRole> VwUserRoles { get; set; }

    public virtual DbSet<UserIntegrityClassification> UserIntegrityClassifications { get; set; }

    public virtual DbSet<ObjectIntegrityClassification> ObjectIntegrityClassifications { get; set; }

    public virtual DbSet<IntegrityClassificationHistory> IntegrityClassificationHistories { get; set; }

    public virtual DbSet<IntegritySecurityAudit> IntegritySecurityAudits { get; set; }

    public virtual DbSet<ClarkWilsonTransaction> ClarkWilsonTransactions { get; set; }

    public virtual DbSet<ClarkWilsonApproval> ClarkWilsonApprovals { get; set; }

    public virtual DbSet<ClarkWilsonAudit> ClarkWilsonAudits { get; set; }

    public virtual DbSet<ClarkWilsonVerificationLog> ClarkWilsonVerificationLogs { get; set; }

    public virtual DbSet<ContactMessage> ContactMessages { get; set; }

    public virtual DbSet<UserFeedback> UserFeedbacks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppRole>(entity =>
        {
            entity.HasKey(e => e.RoleId);

            entity.ToTable("APP_ROLE");

            entity.HasIndex(e => e.RoleName, "UQ_APP_ROLE_NAME").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("Role_ID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())", "DF_APP_ROLE_CREATED")
                .HasColumnName("Created_At");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true, "DF_APP_ROLE_ACTIVE")
                .HasColumnName("Is_Active");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("Role_Name");

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .HasConstraintName("FK_ROLE_PERMISSION_PERMISSION"),
                    l => l.HasOne<AppRole>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("FK_ROLE_PERMISSION_ROLE"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId");
                        j.ToTable("ROLE_PERMISSION");
                        j.IndexerProperty<int>("RoleId").HasColumnName("Role_ID");
                        j.IndexerProperty<int>("PermissionId").HasColumnName("Permission_ID");
                    });
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.ToTable("ATTENDANCE");

            entity.HasIndex(e => new { e.EmployeeId, e.AttendanceDate }, "IX_ATTENDANCE_EMPLOYEE_DATE");

            entity.HasIndex(e => new { e.EmployeeId, e.AttendanceDate }, "UQ_ATTENDANCE_EMPLOYEE_DATE").IsUnique();

            entity.Property(e => e.AttendanceId).HasColumnName("Attendance_ID");
            entity.Property(e => e.AttendanceDate).HasColumnName("Attendance_Date");
            entity.Property(e => e.AttendanceStatus)
                .HasMaxLength(30)
                .HasColumnName("Attendance_Status");
            entity.Property(e => e.CheckInTime).HasColumnName("Check_In_Time");
            entity.Property(e => e.CheckOutTime).HasColumnName("Check_Out_Time");
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.OvertimeHours)
                .HasComputedColumnSql("(CONVERT([decimal](5,2),case when [Check_In_Time] IS NOT NULL AND [Check_Out_Time] IS NOT NULL AND [Check_Out_Time]>=[Check_In_Time] then case when datediff(minute,[Check_In_Time],[Check_Out_Time])>(480) then (datediff(minute,[Check_In_Time],[Check_Out_Time])-(480))/(60.0) else (0) end  end))", true)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("Overtime_Hours");
            entity.Property(e => e.WorkingHours)
                .HasComputedColumnSql("(CONVERT([decimal](5,2),case when [Check_In_Time] IS NOT NULL AND [Check_Out_Time] IS NOT NULL AND [Check_Out_Time]>=[Check_In_Time] then datediff(minute,[Check_In_Time],[Check_Out_Time])/(60.0)  end))", true)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("Working_Hours");

            entity.HasOne(d => d.Employee).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ATTENDANCE_EMPLOYEE");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId);

            entity.ToTable("AUDIT_LOG");

            entity.HasIndex(e => new { e.UserId, e.ActionDateTime }, "IX_AUDIT_LOG_USER_DATETIME");

            entity.Property(e => e.AuditId).HasColumnName("Audit_ID");
            entity.Property(e => e.ActionDateTime)
                .HasDefaultValueSql("(sysdatetime())", "DF_AUDIT_ACTION_DATETIME")
                .HasColumnName("Action_DateTime");
            entity.Property(e => e.ActionStatus)
                .HasMaxLength(30)
                .HasColumnName("Action_Status");
            entity.Property(e => e.ActionType)
                .HasMaxLength(30)
                .HasColumnName("Action_Type");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("IP_Address");
            entity.Property(e => e.RecordId).HasColumnName("Record_ID");
            entity.Property(e => e.TableName)
                .HasMaxLength(128)
                .HasColumnName("Table_Name");
            entity.Property(e => e.UserId).HasColumnName("User_ID");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AUDIT_LOG_USER");
        });

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("BANK_ACCOUNT");

            entity.HasIndex(e => e.EmployeeId, "IX_BANK_ACCOUNT_EMPLOYEE");

            entity.HasIndex(e => e.AccountNumber, "UQ_BANK_ACCOUNT_NUMBER").IsUnique();

            entity.HasIndex(e => e.EmployeeId, "UX_BANK_ACCOUNT_PRIMARY_EMPLOYEE")
                .IsUnique()
                .HasFilter("([Is_Primary]=(1))");

            entity.Property(e => e.BankAccountId).HasColumnName("Bank_Account_ID");
            entity.Property(e => e.AccountNumber)
                .HasMaxLength(50)
                .HasColumnName("Account_Number");
            entity.Property(e => e.AccountType)
                .HasMaxLength(40)
                .HasColumnName("Account_Type");
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.IsPrimary).HasColumnName("Is_Primary");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("Role_Name");

            entity.HasOne(d => d.Employee).WithOne(p => p.BankAccount)
                .HasForeignKey<BankAccount>(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BANK_ACCOUNT_EMPLOYEE");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("DEPARTMENT");

            entity.Property(e => e.DepartmentId).HasColumnName("Department_ID");
            entity.Property(e => e.DepartmentName)
                .HasMaxLength(100)
                .HasColumnName("Department_Name");
            entity.Property(e => e.DepartmentStatus)
                .HasMaxLength(30)
                .HasColumnName("Department_Status");
            entity.Property(e => e.Location).HasMaxLength(150);
        });

        modelBuilder.Entity<Designation>(entity =>
        {
            entity.ToTable("DESIGNATION");

            entity.Property(e => e.DesignationId).HasColumnName("Designation_ID");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.DesignationName)
                .HasMaxLength(100)
                .HasColumnName("Designation_Name");
            entity.Property(e => e.JobLevel)
                .HasMaxLength(20)
                .HasColumnName("Job_Level");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("EMPLOYEE");

            entity.HasIndex(e => e.DepartmentId, "IX_EMPLOYEE_DEPARTMENT");

            entity.HasIndex(e => e.DesignationId, "IX_EMPLOYEE_DESIGNATION");

            entity.HasIndex(e => e.SalaryStructureId, "IX_EMPLOYEE_SALARY_STRUCTURE");

            entity.HasIndex(e => e.Email, "UQ_EMPLOYEE_EMAIL").IsUnique();

            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.DateOfBirth).HasColumnName("Date_of_Birth");
            entity.Property(e => e.DepartmentId).HasColumnName("Department_ID");
            entity.Property(e => e.DesignationId).HasColumnName("Designation_ID");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.EmploymentStatus)
                .HasMaxLength(30)
                .HasColumnName("Employment_Status");
            entity.Property(e => e.FirstName)
                .HasMaxLength(60)
                .HasColumnName("First_Name");
            entity.Property(e => e.Gender).HasMaxLength(20);
            entity.Property(e => e.HireDate).HasColumnName("Hire_Date");
            entity.Property(e => e.LastName)
                .HasMaxLength(60)
                .HasColumnName("Last_Name");
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.SalaryStructureId).HasColumnName("Salary_Structure_ID");

            entity.HasOne(d => d.Department).WithMany(p => p.Employees)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMPLOYEE_DEPARTMENT");

            entity.HasOne(d => d.Designation).WithMany(p => p.Employees)
                .HasForeignKey(d => d.DesignationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMPLOYEE_DESIGNATION");

            entity.HasOne(d => d.SalaryStructure).WithMany(p => p.Employees)
                .HasForeignKey(d => d.SalaryStructureId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMPLOYEE_SALARY_STRUCTURE");
        });

        modelBuilder.Entity<Leave>(entity =>
        {
            entity.ToTable("LEAVE");

            entity.HasIndex(e => e.EmployeeId, "IX_LEAVE_EMPLOYEE");

            entity.Property(e => e.LeaveId).HasColumnName("Leave_ID");
            entity.Property(e => e.ApprovedDate).HasColumnName("Approved_Date");
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.EndDate).HasColumnName("End_Date");
            entity.Property(e => e.LeaveStatus)
                .HasMaxLength(30)
                .HasColumnName("Leave_Status");
            entity.Property(e => e.LeaveType)
                .HasMaxLength(50)
                .HasColumnName("Leave_Type");
            entity.Property(e => e.NumberOfDays)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("Number_of_Days");
            entity.Property(e => e.Reason).HasMaxLength(250);
            entity.Property(e => e.StartDate).HasColumnName("Start_Date");

            entity.HasOne(d => d.Employee).WithMany(p => p.Leaves)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LEAVE_EMPLOYEE");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("PAYMENT");

            entity.HasIndex(e => e.PayrollItemId, "IX_PAYMENT_PAYROLL_ITEM");

            entity.HasIndex(e => e.TransactionReference, "UQ_PAYMENT_TRANSACTION_REFERENCE").IsUnique();

            entity.Property(e => e.PaymentId).HasColumnName("Payment_ID");
            entity.Property(e => e.Amount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.PaymentDate).HasColumnName("Payment_Date");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(40)
                .HasColumnName("Payment_Method");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(30)
                .HasColumnName("Payment_Status");
            entity.Property(e => e.PayrollItemId).HasColumnName("Payroll_Item_ID");
            entity.Property(e => e.TransactionReference)
                .HasMaxLength(100)
                .HasColumnName("Transaction_Reference");

            entity.HasOne(d => d.PayrollItem).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PayrollItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PAYMENT_PAYROLL_ITEM");
        });

        modelBuilder.Entity<Payroll>(entity =>
        {
            entity.ToTable("PAYROLL");

            entity.HasIndex(e => e.EmployeeId, "IX_PAYROLL_EMPLOYEE");

            entity.HasIndex(e => new { e.EmployeeId, e.PayPeriodStart, e.PayPeriodEnd }, "UQ_EMPLOYEE_PAY_PERIOD").IsUnique();

            entity.Property(e => e.PayrollId).HasColumnName("Payroll_ID");
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.GeneratedDate).HasColumnName("Generated_Date");
            entity.Property(e => e.GrossSalary)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Gross_Salary");
            entity.Property(e => e.NetSalary)
                .HasComputedColumnSql("(CONVERT([decimal](12,2),[Gross_Salary]-[Total_Deductions]))", true)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Net_Salary");
            entity.Property(e => e.PayPeriodEnd).HasColumnName("Pay_Period_End");
            entity.Property(e => e.PayPeriodStart).HasColumnName("Pay_Period_Start");
            entity.Property(e => e.PayrollStatus)
                .HasMaxLength(30)
                .HasColumnName("Payroll_Status");
            entity.Property(e => e.TotalDeductions)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Total_Deductions");

            entity.HasOne(d => d.Employee).WithMany(p => p.Payrolls)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PAYROLL_EMPLOYEE");
        });

        modelBuilder.Entity<PayrollItem>(entity =>
        {
            entity.ToTable("PAYROLL_ITEM");

            entity.HasIndex(e => e.PayrollId, "IX_PAYROLL_ITEM_PAYROLL");

            entity.Property(e => e.PayrollItemId).HasColumnName("Payroll_Item_ID");
            entity.Property(e => e.CalculationBasis)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Calculation_Basis");
            entity.Property(e => e.ItemType)
                .HasMaxLength(30)
                .HasColumnName("Item_Type");
            entity.Property(e => e.PayrollId).HasColumnName("Payroll_ID");
            entity.Property(e => e.Quantity).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Payroll).WithMany(p => p.PayrollItems)
                .HasForeignKey(d => d.PayrollId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PAYROLL_ITEM_PAYROLL");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("PERMISSION");

            entity.HasIndex(e => e.PermissionName, "UQ_PERMISSION_NAME").IsUnique();

            entity.Property(e => e.PermissionId).HasColumnName("Permission_ID");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.PermissionName)
                .HasMaxLength(100)
                .HasColumnName("Permission_Name");
        });

        modelBuilder.Entity<SalaryComponent>(entity =>
        {
            entity.HasKey(e => e.ComponentId);

            entity.ToTable("SALARY_COMPONENT");

            entity.HasIndex(e => e.SalaryStructureId, "IX_SALARY_COMPONENT_STRUCTURE");

            entity.Property(e => e.ComponentId).HasColumnName("Component_ID");
            entity.Property(e => e.CalculationType)
                .HasMaxLength(30)
                .HasColumnName("Calculation_Type");
            entity.Property(e => e.ComponentName)
                .HasMaxLength(100)
                .HasColumnName("Component_Name");
            entity.Property(e => e.ComponentType)
                .HasMaxLength(30)
                .HasColumnName("Component_Type");
            entity.Property(e => e.FixedAmount)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Fixed_Amount");
            entity.Property(e => e.Percentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.SalaryStructureId).HasColumnName("Salary_Structure_ID");
            entity.Property(e => e.TaxableFlag).HasColumnName("Taxable_Flag");

            entity.HasOne(d => d.SalaryStructure).WithMany(p => p.SalaryComponents)
                .HasForeignKey(d => d.SalaryStructureId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SALARY_COMPONENT_STRUCTURE");
        });

        modelBuilder.Entity<SalaryStructure>(entity =>
        {
            entity.ToTable("SALARY_STRUCTURE");

            entity.Property(e => e.SalaryStructureId).HasColumnName("Salary_Structure_ID");
            entity.Property(e => e.BasicSalary)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Basic_Salary");
            entity.Property(e => e.EffectiveFrom).HasColumnName("Effective_From");
            entity.Property(e => e.EffectiveTo).HasColumnName("Effective_To");
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.StructureName)
                .HasMaxLength(100)
                .HasColumnName("Structure_Name");
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("USER_ACCOUNT");

            entity.HasIndex(e => e.EmployeeId, "UQ_USER_ACCOUNT_EMPLOYEE").IsUnique();

            entity.HasIndex(e => e.Username, "UQ_USER_ACCOUNT_USERNAME").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("User_ID");
            entity.Property(e => e.AccountStatus)
                .HasMaxLength(30)
                .HasColumnName("Account_Status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())", "DF_USER_CREATED_AT")
                .HasColumnName("Created_At");
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.FailedLoginAttempts).HasColumnName("Failed_Login_Attempts");
            entity.Property(e => e.LockoutEndUtc).HasColumnName("LockoutEndUtc");
            entity.Property(e => e.LastLogin).HasColumnName("Last_Login");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Password_Hash");
            entity.Property(e => e.Username).HasMaxLength(80);

            entity.Property(e => e.MfaEnabled).HasColumnName("MfaEnabled").HasDefaultValue(false);
            entity.Property(e => e.MfaType).HasMaxLength(30).HasColumnName("MfaType");
            entity.Property(e => e.MfaSecretKey).HasMaxLength(1000).HasColumnName("MfaSecretKey");
            entity.Property(e => e.MfaRecoveryCodes).HasMaxLength(2000).HasColumnName("MfaRecoveryCodes");
            entity.Property(e => e.EmailConfirmed).HasColumnName("EmailConfirmed").HasDefaultValue(false);
            entity.Property(e => e.EmailOtpCode).HasMaxLength(500).HasColumnName("EmailOtpCode");
            entity.Property(e => e.EmailOtpExpiry).HasColumnName("EmailOtpExpiry");
            entity.Property(e => e.EmailOtpAttempts).HasColumnName("EmailOtpAttempts").HasDefaultValue(0);
            entity.Property(e => e.EmailVerificationTokenHash).HasMaxLength(256).HasColumnName("EmailVerificationTokenHash");
            entity.Property(e => e.EmailVerificationTokenExpiry).HasColumnName("EmailVerificationTokenExpiry");
            entity.Property(e => e.PasswordResetTokenHash).HasMaxLength(256).HasColumnName("PasswordResetTokenHash");
            entity.Property(e => e.PasswordResetTokenExpiry).HasColumnName("PasswordResetTokenExpiry");

            entity.HasOne(d => d.Employee).WithOne(p => p.UserAccount)
                .HasForeignKey<UserAccount>(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USER_ACCOUNT_EMPLOYEE");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity.ToTable("USER_ROLE");

            entity.HasIndex(e => e.RoleId, "IX_USER_ROLE_ROLE");

            entity.Property(e => e.UserId).HasColumnName("User_ID");
            entity.Property(e => e.RoleId).HasColumnName("Role_ID");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(sysdatetime())", "DF_USER_ROLE_ASSIGNED_AT")
                .HasColumnName("Assigned_At");
            entity.Property(e => e.AssignedBy).HasColumnName("Assigned_By");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.UserRoleAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK_USER_ROLE_ASSIGNED_BY");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USER_ROLE_ROLE");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoleUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_USER_ROLE_USER");
        });

        modelBuilder.Entity<VwAuditReport>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_AuditReport");

            entity.Property(e => e.ActionDateTime).HasColumnName("Action_DateTime");
            entity.Property(e => e.ActionStatus)
                .HasMaxLength(30)
                .HasColumnName("Action_Status");
            entity.Property(e => e.ActionType)
                .HasMaxLength(30)
                .HasColumnName("Action_Type");
            entity.Property(e => e.AuditId).HasColumnName("Audit_ID");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("IP_Address");
            entity.Property(e => e.RecordId).HasColumnName("Record_ID");
            entity.Property(e => e.TableName)
                .HasMaxLength(128)
                .HasColumnName("Table_Name");
            entity.Property(e => e.Username).HasMaxLength(80);
        });

        modelBuilder.Entity<VwMyEmployeeProfile>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_MyEmployeeProfile");

            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.DepartmentName)
                .HasMaxLength(100)
                .HasColumnName("Department_Name");
            entity.Property(e => e.DesignationName)
                .HasMaxLength(100)
                .HasColumnName("Designation_Name");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.EmploymentStatus)
                .HasMaxLength(30)
                .HasColumnName("Employment_Status");
            entity.Property(e => e.FirstName)
                .HasMaxLength(60)
                .HasColumnName("First_Name");
            entity.Property(e => e.HireDate).HasColumnName("Hire_Date");
            entity.Property(e => e.LastName)
                .HasMaxLength(60)
                .HasColumnName("Last_Name");
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.UserId).HasColumnName("User_ID");
            entity.Property(e => e.Username).HasMaxLength(80);
        });

        modelBuilder.Entity<VwPayrollSummary>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PayrollSummary");

            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.EmployeeName)
                .HasMaxLength(121)
                .HasColumnName("Employee_Name");
            entity.Property(e => e.GeneratedDate).HasColumnName("Generated_Date");
            entity.Property(e => e.GrossSalary)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Gross_Salary");
            entity.Property(e => e.NetSalary)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Net_Salary");
            entity.Property(e => e.PayPeriodEnd).HasColumnName("Pay_Period_End");
            entity.Property(e => e.PayPeriodStart).HasColumnName("Pay_Period_Start");
            entity.Property(e => e.PayrollId).HasColumnName("Payroll_ID");
            entity.Property(e => e.PayrollStatus)
                .HasMaxLength(30)
                .HasColumnName("Payroll_Status");
            entity.Property(e => e.TotalDeductions)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("Total_Deductions");
        });

        modelBuilder.Entity<VwRolePermission>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_RolePermissions");

            entity.Property(e => e.PermissionDescription)
                .HasMaxLength(250)
                .HasColumnName("Permission_Description");
            entity.Property(e => e.PermissionId).HasColumnName("Permission_ID");
            entity.Property(e => e.PermissionName)
                .HasMaxLength(100)
                .HasColumnName("Permission_Name");
            entity.Property(e => e.RoleId).HasColumnName("Role_ID");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("Role_Name");
        });

        modelBuilder.Entity<VwUserRole>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_UserRoles");

            entity.Property(e => e.AssignedAt).HasColumnName("Assigned_At");
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.EmployeeName)
                .HasMaxLength(121)
                .HasColumnName("Employee_Name");
            entity.Property(e => e.RoleDescription)
                .HasMaxLength(250)
                .HasColumnName("Role_Description");
            entity.Property(e => e.RoleId).HasColumnName("Role_ID");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("Role_Name");
            entity.Property(e => e.UserId).HasColumnName("User_ID");
            entity.Property(e => e.Username).HasMaxLength(80);
        });

        modelBuilder.Entity<UserIntegrityClassification>(entity =>
        {
            entity.HasKey(e => e.UserIntegrityId);
            entity.ToTable("USER_INTEGRITY_CLASSIFICATION");
            entity.Property(e => e.UserIntegrityId).HasColumnName("User_Integrity_ID");
            entity.Property(e => e.UserId).HasColumnName("User_ID");
            entity.Property(e => e.IntegrityLevel).HasColumnName("Integrity_Level");
            entity.Property(e => e.AssignedBy).HasColumnName("Assigned_By");
            entity.Property(e => e.AssignedAtUtc).HasColumnName("Assigned_At_Utc").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(250);
            entity.Property(e => e.IsActive).HasColumnName("Is_Active").HasDefaultValue(true);

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_USER_INTEGRITY_USER");

            entity.HasOne(d => d.AssigningUser).WithMany()
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK_USER_INTEGRITY_ASSIGNED_BY");
        });

        modelBuilder.Entity<ObjectIntegrityClassification>(entity =>
        {
            entity.HasKey(e => e.ObjectIntegrityId);
            entity.ToTable("OBJECT_INTEGRITY_CLASSIFICATION");
            entity.Property(e => e.ObjectIntegrityId).HasColumnName("Object_Integrity_ID");
            entity.Property(e => e.EntityType).HasMaxLength(50).HasColumnName("Entity_Type");
            entity.Property(e => e.EntityId).HasColumnName("Entity_ID");
            entity.Property(e => e.IntegrityLevel).HasColumnName("Integrity_Level");
            entity.Property(e => e.AssignedBy).HasColumnName("Assigned_By");
            entity.Property(e => e.AssignedAtUtc).HasColumnName("Assigned_At_Utc").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(250);
            entity.Property(e => e.IsActive).HasColumnName("Is_Active").HasDefaultValue(true);

            entity.HasOne(d => d.AssigningUser).WithMany()
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK_OBJECT_INTEGRITY_ASSIGNED_BY");
        });

        modelBuilder.Entity<IntegrityClassificationHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId);
            entity.ToTable("INTEGRITY_CLASSIFICATION_HISTORY");
            entity.Property(e => e.HistoryId).HasColumnName("History_ID");
            entity.Property(e => e.EntityType).HasMaxLength(50).HasColumnName("Entity_Type");
            entity.Property(e => e.EntityId).HasColumnName("Entity_ID");
            entity.Property(e => e.PreviousIntegrityLevel).HasColumnName("Previous_Integrity_Level");
            entity.Property(e => e.NewIntegrityLevel).HasColumnName("New_Integrity_Level");
            entity.Property(e => e.ChangedBy).HasColumnName("Changed_By");
            entity.Property(e => e.ChangedAtUtc).HasColumnName("Changed_At_Utc").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ApprovalReference).HasMaxLength(100).HasColumnName("Approval_Reference");

            entity.HasOne(d => d.ChangingUser).WithMany()
                .HasForeignKey(d => d.ChangedBy)
                .HasConstraintName("FK_INTEGRITY_HISTORY_CHANGED_BY");
        });

        modelBuilder.Entity<IntegritySecurityAudit>(entity =>
        {
            entity.HasKey(e => e.AuditId);
            entity.ToTable("INTEGRITY_SECURITY_AUDIT");
            entity.Property(e => e.AuditId).HasColumnName("Audit_ID");
            entity.Property(e => e.UserId).HasColumnName("User_ID");
            entity.Property(e => e.Operation).HasMaxLength(30);
            entity.Property(e => e.EntityType).HasMaxLength(50).HasColumnName("Entity_Type");
            entity.Property(e => e.EntityId).HasColumnName("Entity_ID");
            entity.Property(e => e.SubjectIntegrityLevel).HasColumnName("Subject_Integrity_Level");
            entity.Property(e => e.ObjectIntegrityLevel).HasColumnName("Object_Integrity_Level");
            entity.Property(e => e.Decision).HasMaxLength(20);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45).IsUnicode(false).HasColumnName("IP_Address");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("Created_At_Utc").HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_INTEGRITY_AUDIT_USER");
        });

        modelBuilder.Entity<ClarkWilsonTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId);
            entity.ToTable("CLARK_WILSON_TRANSACTION");
            entity.Property(e => e.TransactionId).HasColumnName("Transaction_ID");
            entity.Property(e => e.TransactionType).HasMaxLength(50).HasColumnName("Transaction_Type");
            entity.Property(e => e.TargetEntityType).HasMaxLength(50).HasColumnName("Target_Entity_Type");
            entity.Property(e => e.TargetEntityId).HasColumnName("Target_Entity_ID");
            entity.Property(e => e.PayloadJson).HasColumnName("Payload_Json");
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("PENDING");
            entity.Property(e => e.SubmittedByUserId).HasColumnName("Submitted_By_User_ID");
            entity.Property(e => e.SubmittedAtUtc).HasColumnName("Submitted_At_Utc").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Justification).HasMaxLength(500);
            entity.Property(e => e.ConcurrencyToken).IsRowVersion().HasColumnName("Concurrency_Token");

            entity.HasOne(d => d.SubmittedByUser).WithMany()
                .HasForeignKey(d => d.SubmittedByUserId)
                .HasConstraintName("FK_CW_TX_SUBMITTED_BY");
        });

        modelBuilder.Entity<ClarkWilsonApproval>(entity =>
        {
            entity.HasKey(e => e.ApprovalId);
            entity.ToTable("CLARK_WILSON_APPROVAL");
            entity.Property(e => e.ApprovalId).HasColumnName("Approval_ID");
            entity.Property(e => e.TransactionId).HasColumnName("Transaction_ID");
            entity.Property(e => e.ApproverUserId).HasColumnName("Approver_User_ID");
            entity.Property(e => e.Decision).HasMaxLength(20);
            entity.Property(e => e.DecisionAtUtc).HasColumnName("Decision_At_Utc").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Remarks).HasMaxLength(500);

            entity.HasOne(d => d.Transaction).WithMany(p => p.Approvals)
                .HasForeignKey(d => d.TransactionId)
                .HasConstraintName("FK_CW_APPROVAL_TX");

            entity.HasOne(d => d.ApproverUser).WithMany()
                .HasForeignKey(d => d.ApproverUserId)
                .HasConstraintName("FK_CW_APPROVAL_USER");
        });

        modelBuilder.Entity<ClarkWilsonAudit>(entity =>
        {
            entity.HasKey(e => e.AuditId);
            entity.ToTable("CLARK_WILSON_AUDIT");
            entity.Property(e => e.AuditId).HasColumnName("Audit_ID");
            entity.Property(e => e.UserId).HasColumnName("User_ID");
            entity.Property(e => e.EventType).HasMaxLength(60).HasColumnName("Event_Type");
            entity.Property(e => e.TargetEntity).HasMaxLength(50).HasColumnName("Target_Entity");
            entity.Property(e => e.TargetEntityId).HasColumnName("Target_Entity_ID");
            entity.Property(e => e.Outcome).HasMaxLength(20);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(50).HasColumnName("Ip_Address");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("Created_At_Utc").HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_CW_AUDIT_USER");
        });

        modelBuilder.Entity<ClarkWilsonVerificationLog>(entity =>
        {
            entity.HasKey(e => e.LogId);
            entity.ToTable("CLARK_WILSON_VERIFICATION_LOG");
            entity.Property(e => e.LogId).HasColumnName("Log_ID");
            entity.Property(e => e.IvpName).HasMaxLength(60).HasColumnName("IVP_Name");
            entity.Property(e => e.TargetEntity).HasMaxLength(50).HasColumnName("Target_Entity");
            entity.Property(e => e.TargetEntityId).HasColumnName("Target_Entity_ID");
            entity.Property(e => e.IsConsistent).HasColumnName("Is_Consistent");
            entity.Property(e => e.ValidationErrors).HasColumnName("Validation_Errors");
            entity.Property(e => e.VerifiedAtUtc).HasColumnName("Verified_At_Utc").HasDefaultValueSql("(sysutcdatetime())");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
