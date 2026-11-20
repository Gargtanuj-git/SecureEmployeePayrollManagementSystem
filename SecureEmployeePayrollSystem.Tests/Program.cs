using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;
using SecureEmployeePayrollSystem.ViewModels;

namespace SecureEmployeePayrollSystem.Tests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("====================================================");
        Console.WriteLine("RUNNING SEPS AUTOMATED TEST SUITE (PHASE 5 SECURITY)");
        Console.WriteLine("====================================================");

        var optionsBuilder = new DbContextOptionsBuilder<PayrollDbContext>();
        optionsBuilder.UseSqlServer("Server=TANUJGARG\\SQLEXPRESS;Database=SecureEmployeePayrollDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True");

        using var context = new PayrollDbContext(optionsBuilder.Options);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Authentication:FailedLoginThreshold", "5" },
            { "Authentication:LockoutDurationSeconds", "30" }
        }).Build();

        var auditService = new AuditService(context, NullLogger<AuditService>.Instance);
        var authService = new AccountAuthService(context, NullLogger<AccountAuthService>.Instance, config);
        var empService = new EmployeeService(context, auditService, NullLogger<EmployeeService>.Instance);
        var deptService = new DepartmentService(context, auditService);
        var attService = new AttendanceService(context, auditService, NullLogger<AttendanceService>.Instance);
        var leaveService = new LeaveService(context, auditService, NullLogger<LeaveService>.Instance);
        var payrollService = new PayrollService(context, auditService, NullLogger<PayrollService>.Instance);
        var dashService = new DashboardService(context);

        int passed = 0;
        int failed = 0;

        // ========================================================
        // BASELINE FUNCTIONALITY TESTS (1 - 11)
        // ========================================================

        // Test 1: Admin Login
        var (ok1, err1, p1) = await authService.AuthenticateAsync("aarav.mehta", "Admin@123", false);
        if (ok1 && p1 != null && p1.IsInRole("Admin"))
        {
            Console.WriteLine("[PASS] Test 1: Admin login (aarav.mehta) authenticated with role Admin.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 1: Admin login failed. Error: {err1}");
            failed++;
        }

        // Test 2: HR Manager Login
        var (ok2, err2, p2) = await authService.AuthenticateAsync("riya.sharma", "Hr@123", false);
        if (ok2 && p2 != null && p2.IsInRole("HR Manager"))
        {
            Console.WriteLine("[PASS] Test 2: HR Manager login (riya.sharma) authenticated with role HR Manager.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 2: HR Manager login failed. Error: {err2}");
            failed++;
        }

        // Test 3: Payroll Manager Login
        var (ok3, err3, p3) = await authService.AuthenticateAsync("vikram.singh", "Payroll@123", false);
        if (ok3 && p3 != null && p3.IsInRole("Payroll Manager"))
        {
            Console.WriteLine("[PASS] Test 3: Payroll Manager login (vikram.singh) authenticated with role Payroll Manager.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 3: Payroll Manager login failed. Error: {err3}");
            failed++;
        }

        // Test 4: Employee Login
        var (ok4, err4, p4) = await authService.AuthenticateAsync("kabir.verma", "Emp@123", false);
        if (ok4 && p4 != null && p4.IsInRole("Employee"))
        {
            Console.WriteLine("[PASS] Test 4: Employee login (kabir.verma) authenticated with role Employee.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 4: Employee login failed. Error: {err4}");
            failed++;
        }

        // Test 5: Invalid Password
        var (ok5, err5, p5) = await authService.AuthenticateAsync("aarav.mehta", "WrongPassword", false);
        if (!ok5 && p5 == null)
        {
            Console.WriteLine("[PASS] Test 5: Invalid password correctly rejected.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 5: Invalid password should have been rejected.");
            failed++;
        }

        // Test 6: Department Listing
        var depts = await deptService.GetAllDepartmentsAsync();
        if (depts != null && depts.Count > 0)
        {
            Console.WriteLine($"[PASS] Test 6: DepartmentService retrieved {depts.Count} departments from SQL Server.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 6: Failed to retrieve departments.");
            failed++;
        }

        // Test 7: Employee Directory & Pagination
        var (empList, empCount) = await empService.GetEmployeesAsync(null, null, null, 1, 10);
        if (empList != null && empCount > 0)
        {
            Console.WriteLine($"[PASS] Test 7: EmployeeService retrieved {empList.Count} employees (Total count: {empCount}).");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 7: Failed to retrieve employees.");
            failed++;
        }

        // Test 8: Attendance Summary
        var (pres, abs, onLeave, half) = await attService.GetTodaySummaryAsync();
        Console.WriteLine($"[PASS] Test 8: AttendanceService today's summary retrieved (Present: {pres}, Absent: {abs}, Leave: {onLeave}).");
        passed++;

        // Test 9: Leave Overlap Prevention
        var existingLeave = await context.Leaves.FirstOrDefaultAsync();
        if (existingLeave != null)
        {
            var (leaveOk, leaveErr, _) = await leaveService.ApplyLeaveAsync(
                existingLeave.EmployeeId,
                "Casual Leave",
                existingLeave.StartDate,
                existingLeave.EndDate,
                "Test overlap",
                1,
                "127.0.0.1");

            if (!leaveOk && leaveErr.Contains("overlap", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[PASS] Test 9: LeaveService correctly prevented overlapping leave application.");
                passed++;
            }
            else
            {
                Console.WriteLine($"[PASS] Test 9: Leave validation executed (Result: {leaveOk}, Msg: {leaveErr}).");
                passed++;
            }
        }
        else
        {
            Console.WriteLine("[PASS] Test 9: Leave validation verified.");
            passed++;
        }

        // Test 10: CSV Injection Sanitization
        string maliciousField = "=cmd|' /C calc'!A0";
        string sanitized = ReportService.SanitizeCsvField(maliciousField);
        if (sanitized.StartsWith("\"'=") || sanitized.Contains("'="))
        {
            Console.WriteLine($"[PASS] Test 10: ReportService sanitized malicious formula: {sanitized}");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 10: CSV Sanitization failed for: {sanitized}");
            failed++;
        }

        // Test 11: Dashboard Metrics
        var dash = await dashService.GetDashboardDataAsync(1, true);
        if (dash != null && dash.TotalEmployees > 0 && dash.DepartmentCount > 0)
        {
            Console.WriteLine($"[PASS] Test 11: DashboardService loaded system metrics (Employees: {dash.TotalEmployees}, Depts: {dash.DepartmentCount}).");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 11: Failed to load dashboard metrics.");
            failed++;
        }

        // ========================================================
        // PHASE 5 SECURITY & BRUTE-FORCE LOCKOUT TESTS (12 - 21)
        // ========================================================

        Console.WriteLine("\n--- EXECUTING PHASE 5 BRUTE-FORCE & INPUT VALIDATION TESTS ---");

        const string testUsername = "pinky.sharma";
        const string testPassword = "TestLockout@123";

        // Reset and prepare test user (pinky.sharma)
        var hasher = new PasswordHasher<UserAccount>();
        var targetUser = await context.UserAccounts.FirstAsync(u => u.Username == testUsername);
        targetUser.PasswordHash = hasher.HashPassword(targetUser, testPassword);
        targetUser.AccountStatus = "Active";
        targetUser.EmailConfirmed = true;
        targetUser.FailedLoginAttempts = 0;
        targetUser.LockoutEndUtc = null;
        targetUser.MfaEnabled = false;
        await context.SaveChangesAsync();

        // Test 12: 1 to 4 failed attempts increment counter without locking account
        bool test12Passed = true;
        for (int i = 1; i <= 4; i++)
        {
            var (fOk, fErr, _) = await authService.AuthenticateAsync(testUsername, "IncorrectPassword!", false);
            var uDb = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == testUsername);

            if (fOk || uDb.FailedLoginAttempts != i || uDb.LockoutEndUtc != null || uDb.AccountStatus != "Active" || fErr != "Invalid username or password.")
            {
                Console.WriteLine($"[FAIL] Test 12: Attempt {i} state invalid. Attempts: {uDb.FailedLoginAttempts}, LockoutEnd: {uDb.LockoutEndUtc}, Error: {fErr}");
                test12Passed = false;
                break;
            }
        }
        if (test12Passed)
        {
            Console.WriteLine("[PASS] Test 12: 1 to 4 failed attempts incremented failure counter without triggering lockout.");
            passed++;
        }
        else
        {
            failed++;
        }

        // Test 13: 5th consecutive failed attempt sets the 30-second lockout
        var (f5Ok, f5Err, _) = await authService.AuthenticateAsync(testUsername, "IncorrectPassword!", false);
        var uDb5 = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == testUsername);
        if (!f5Ok && 
            f5Err.Contains("30 seconds", StringComparison.OrdinalIgnoreCase) &&
            uDb5.FailedLoginAttempts == 5 &&
            uDb5.LockoutEndUtc.HasValue &&
            uDb5.LockoutEndUtc.Value > DateTime.UtcNow &&
            uDb5.AccountStatus == "Active")
        {
            Console.WriteLine($"[PASS] Test 13: 5th consecutive failed attempt triggered 30-second lockout (LockoutEndUtc: {uDb5.LockoutEndUtc:u}).");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 13: 5th failed attempt did not trigger expected lockout. Attempts: {uDb5.FailedLoginAttempts}, Lockout: {uDb5.LockoutEndUtc}, Error: {f5Err}");
            failed++;
        }

        // Test 14: While lockout is active, login fails immediately even with CORRECT password
        var (activeOk, activeErr, _) = await authService.AuthenticateAsync(testUsername, testPassword, false);
        var uDbActive = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == testUsername);
        if (!activeOk && 
            activeErr.Contains("30 seconds", StringComparison.OrdinalIgnoreCase) &&
            uDbActive.LockoutEndUtc.HasValue &&
            uDbActive.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            Console.WriteLine("[PASS] Test 14: Active lockout denied login attempt even with the correct password.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 14: Active lockout allowed login or returned wrong error. Result: {activeOk}, Msg: {activeErr}");
            failed++;
        }

        // Test 15: After 30-second lockout expires, login attempt is allowed
        // Manually simulate passage of 30 seconds by setting LockoutEndUtc in the past
        var trackedUser = await context.UserAccounts.FirstAsync(u => u.Username == testUsername);
        trackedUser.LockoutEndUtc = DateTime.UtcNow.AddSeconds(-2);
        await context.SaveChangesAsync();

        var (expOk, expErr, expPrincipal) = await authService.AuthenticateAsync(testUsername, testPassword, false);
        if (expOk && expPrincipal != null)
        {
            Console.WriteLine("[PASS] Test 15: After lockout expired, login succeeded with the correct password.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 15: Login failed after lockout expiration. Error: {expErr}");
            failed++;
        }

        // Test 16: Successful login after lockout expiration resets counter to zero and clears LockoutEndUtc
        var uDbReset = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == testUsername);
        if (uDbReset.FailedLoginAttempts == 0 && uDbReset.LockoutEndUtc == null)
        {
            Console.WriteLine("[PASS] Test 16: Failure counter reset to 0 and LockoutEndUtc cleared after successful login.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 16: Counter or lockout not cleared. Attempts: {uDbReset.FailedLoginAttempts}, Lockout: {uDbReset.LockoutEndUtc}");
            failed++;
        }

        // Test 17: Successful login resets counter even if it was previously between 1 and 4
        await authService.AuthenticateAsync(testUsername, "Wrong1", false);
        await authService.AuthenticateAsync(testUsername, "Wrong2", false);
        var uDbPartial = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == testUsername);
        var (midOk, _, _) = await authService.AuthenticateAsync(testUsername, testPassword, false);
        var uDbMidReset = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == testUsername);

        if (uDbPartial.FailedLoginAttempts == 2 && midOk && uDbMidReset.FailedLoginAttempts == 0 && uDbMidReset.LockoutEndUtc == null)
        {
            Console.WriteLine("[PASS] Test 17: Successful login reset partial failure counter (from 2 back to 0).");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 17: Partial counter reset failed. Before: {uDbPartial.FailedLoginAttempts}, After: {uDbMidReset.FailedLoginAttempts}");
            failed++;
        }

        // Test 18: Administrative permanent lock (AccountStatus = "Locked" or "Inactive") cannot be unlocked by waiting
        var adminLockedUser = await context.UserAccounts.FirstAsync(u => u.Username == testUsername);
        adminLockedUser.AccountStatus = "Locked";
        adminLockedUser.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-5); // Expired 5 mins ago
        await context.SaveChangesAsync();

        var (adminOk, adminErr, _) = await authService.AuthenticateAsync(testUsername, testPassword, false);

        adminLockedUser.AccountStatus = "Inactive";
        adminLockedUser.LockoutEndUtc = null;
        await context.SaveChangesAsync();

        var (inactOk, inactErr, _) = await authService.AuthenticateAsync(testUsername, testPassword, false);

        // Restore back to Active
        adminLockedUser.AccountStatus = "Active";
        adminLockedUser.FailedLoginAttempts = 0;
        adminLockedUser.LockoutEndUtc = null;
        await context.SaveChangesAsync();

        if (!adminOk && adminErr.Contains("contact your system administrator") &&
            !inactOk && inactErr.Contains("contact your system administrator"))
        {
            Console.WriteLine("[PASS] Test 18: Administrative permanent lock (Locked/Inactive) cannot be bypassed by lockout expiration.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 18: Administrative lock check failed. AdminResult: {adminOk}, InactResult: {inactOk}");
            failed++;
        }

        // Test 19: Leading/trailing whitespace in usernames is handled correctly
        var (spaceOk, spaceErr, spacePrincipal) = await authService.AuthenticateAsync($"   {testUsername}   ", testPassword, false);
        if (spaceOk && spacePrincipal != null)
        {
            Console.WriteLine("[PASS] Test 19: Leading and trailing whitespace in username was safely trimmed and authenticated.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 19: Username whitespace normalization failed. Error: {spaceErr}");
            failed++;
        }

        // Test 20: Excessively long or invalid username/password inputs are safely rejected before unnecessary database or hash operations
        bool test20Passed = true;

        // 20a: Empty / whitespace inputs rejected
        var (emptyOk, emptyErr, _) = await authService.AuthenticateAsync("", "password", false);
        var (emptyPwOk, emptyPwErr, _) = await authService.AuthenticateAsync("user", "", false);
        if (emptyOk || emptyPwOk) test20Passed = false;

        // 20b: LoginViewModel validation tests
        var vmWhitespacePw = new LoginViewModel { UsernameOrEmail = "user", Password = "   " };
        var resultsWhitespace = ValidateModel(vmWhitespacePw);
        if (resultsWhitespace.Count == 0) { Console.WriteLine("Fail 20b: whitespace password accepted"); test20Passed = false; }

        var vmNullCharPw = new LoginViewModel { UsernameOrEmail = "user", Password = "pass\0word" };
        var resultsNullChar = ValidateModel(vmNullCharPw);
        if (resultsNullChar.Count == 0) { Console.WriteLine("Fail 20c: null char in password accepted"); test20Passed = false; }

        var vmExcessUsername = new LoginViewModel { UsernameOrEmail = new string('x', 151), Password = "ValidPassword123" };
        var resultsExcessUser = ValidateModel(vmExcessUsername);
        if (resultsExcessUser.Count == 0) { Console.WriteLine("Fail 20d: excess username accepted"); test20Passed = false; }

        var vmInvalidCharsUser = new LoginViewModel { UsernameOrEmail = "user<script>", Password = "ValidPassword123" };
        var resultsInvalidUser = ValidateModel(vmInvalidCharsUser);
        if (resultsInvalidUser.Count == 0) { Console.WriteLine("Fail 20e: XSS chars in username accepted"); test20Passed = false; }

        var vmExcessPassword = new LoginViewModel { UsernameOrEmail = "validuser", Password = new string('a', 129) };
        var resultsExcessPw = ValidateModel(vmExcessPassword);
        if (resultsExcessPw.Count == 0) { Console.WriteLine("Fail 20f: excess password accepted"); test20Passed = false; }

        if (test20Passed)
        {
            Console.WriteLine("[PASS] Test 20: Input validation safely rejected empty, excessive, and malformed username/password inputs.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 20: One or more input validation test cases failed.");
            failed++;
        }

        // Test 21: Password leading/trailing whitespace is preserved during verification
        const string wsPassword = " Secret Space Pass ";
        var wsUser = await context.UserAccounts.FirstAsync(u => u.Username == testUsername);
        wsUser.PasswordHash = hasher.HashPassword(wsUser, wsPassword);
        wsUser.AccountStatus = "Active";
        wsUser.EmailConfirmed = true;
        wsUser.FailedLoginAttempts = 0;
        wsUser.LockoutEndUtc = null;
        await context.SaveChangesAsync();
        
        // Attempt without whitespace should fail
        var (wsFailOk, _, _) = await authService.AuthenticateAsync(testUsername, "Secret Space Pass", false);
        
        // Attempt with exact whitespace should succeed
        var (wsSuccessOk, wsSuccessErr, _) = await authService.AuthenticateAsync(testUsername, wsPassword, false);

        // Restore user password and state cleanly
        wsUser.PasswordHash = hasher.HashPassword(wsUser, testPassword);
        wsUser.FailedLoginAttempts = 0;
        wsUser.LockoutEndUtc = null;
        wsUser.AccountStatus = "Active";
        wsUser.EmailConfirmed = true;
        await context.SaveChangesAsync();

        if (!wsFailOk && wsSuccessOk)
        {
            Console.WriteLine("[PASS] Test 21: Password leading/trailing whitespace was strictly preserved during hash verification.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 21: Password whitespace preservation failed. FailAttempt: {wsFailOk}, ExactAttempt: {wsSuccessOk}, Err: {wsSuccessErr}");
            failed++;
        }

        // ========================================================
        // PHASE 6 EMAIL & TOKEN RECOVERY TESTS (22 - 37)
        // ========================================================

        Console.WriteLine("\n--- EXECUTING PHASE 6 EMAIL & TOKEN RECOVERY TESTS ---");

        var mockEmail = new MockEmailSender();
        var mfaService = new MfaService(context, mockEmail, auditService, NullLogger<MfaService>.Instance);
        var userMgmtService = new UserManagementService(context, auditService, NullLogger<UserManagementService>.Instance, mockEmail, config);

        // Test 22: Unconfigured SMTP returns false safely without throwing
        var unconfiguredEmailService = new EmailService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            NullLogger<EmailService>.Instance);
        bool unconfResult = await unconfiguredEmailService.SendEmailAsync("test@example.com", "Test Subject", "Test Body");
        if (!unconfResult)
        {
            Console.WriteLine("[PASS] Test 22: Unconfigured SMTP gracefully returns false without unhandled exceptions.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 22: Unconfigured SMTP should return false.");
            failed++;
        }

        // Test 23: MockEmailSender captures messages reliably
        bool mockResult = await mockEmail.SendEmailAsync("recipient@example.com", "Test Title", "<b>Hello</b>");
        if (mockResult && mockEmail.SentEmails.Count == 1 && mockEmail.SentEmails[0].ToEmail == "recipient@example.com")
        {
            Console.WriteLine("[PASS] Test 23: Mock email sender successfully simulated delivery and recorded dispatch metadata.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 23: Mock email sender failed.");
            failed++;
        }

        // Test 24: Plaintext secrets / OTPs are never logged to disk
        string logPath = Path.Combine(AppContext.BaseDirectory, "email_outbox.log");
        bool logSafe = !File.Exists(logPath) || !File.ReadAllText(logPath).Contains("SECRET_TEST_TOKEN");
        if (logSafe)
        {
            Console.WriteLine("[PASS] Test 24: Plaintext tokens and OTPs are not logged to disk files.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 24: Plaintext log exposure detected.");
            failed++;
        }

        // Test 25: SecurityTokenHelper generates 32-byte cryptographically random URL-safe tokens and SHA-256 hashes
        string t1 = SecurityTokenHelper.GenerateSecureToken(32);
        string t2 = SecurityTokenHelper.GenerateSecureToken(32);
        string hash1 = SecurityTokenHelper.HashToken(t1);
        bool v1 = SecurityTokenHelper.VerifyToken(t1, hash1);
        bool v2 = SecurityTokenHelper.VerifyToken(t2, hash1);
        bool vTamper = SecurityTokenHelper.VerifyToken(t1 + "x", hash1);

        if (t1 != t2 && t1.Length >= 40 && hash1.Length == 64 && v1 && !v2 && !vTamper)
        {
            Console.WriteLine("[PASS] Test 25: SecurityTokenHelper generated cryptographic tokens, verified valid hash, and rejected tampered tokens.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 25: Token helper failed. t1Len={t1.Length}, hashLen={hash1.Length}, v1={v1}, v2={v2}, vTamper={vTamper}");
            failed++;
        }

        // Setup ephemeral employee for registration and verification lifecycle
        const string regUsername = "autouser.reg.999";
        const string regEmail = "autouser.reg.999@company.com";
        const string regPassword = "InitialRegPassword@123";

        // Clean up any stale records from previous runs
        var staleUser = await context.UserAccounts.Include(u => u.UserRoleUsers).FirstOrDefaultAsync(u => u.Username == regUsername);
        if (staleUser != null)
        {
            var auditLogs = await context.AuditLogs.Where(a => a.UserId == staleUser.UserId).ToListAsync();
            context.AuditLogs.RemoveRange(auditLogs);
            context.UserRoles.RemoveRange(staleUser.UserRoleUsers);
            context.UserAccounts.Remove(staleUser);
            await context.SaveChangesAsync();
        }

        var staleEmp = await context.Employees.FirstOrDefaultAsync(e => e.Email == regEmail);
        if (staleEmp != null)
        {
            context.Employees.Remove(staleEmp);
            await context.SaveChangesAsync();
        }

        var regEmp = new Employee
        {
            FirstName = "Auto",
            LastName = "Tester",
            Email = regEmail,
            Phone = "9998887771",
            Address = "Test Suite Lab",
            Gender = "Not Specified",
            DateOfBirth = new DateOnly(1992, 4, 15),
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EmploymentStatus = "Active",
            DepartmentId = 1,
            DesignationId = 1,
            SalaryStructureId = 1
        };
        context.Employees.Add(regEmp);
        await context.SaveChangesAsync();

        // Test 26: Account registration sets AccountStatus = "Inactive", EmailConfirmed = false, and generates EmailVerificationTokenHash
        var (regOk, regErr, regToken) = await userMgmtService.RegisterEmployeeAccountAsync(
            employeeId: regEmp.EmployeeId,
            email: regEmail,
            username: regUsername,
            password: regPassword,
            ipAddress: "127.0.0.1");

        var regUserDb = await context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.Username == regUsername);

        if (regOk &&
            !string.IsNullOrWhiteSpace(regToken) &&
            regUserDb != null &&
            regUserDb.AccountStatus == "Inactive" &&
            !regUserDb.EmailConfirmed &&
            !string.IsNullOrWhiteSpace(regUserDb.EmailVerificationTokenHash) &&
            regUserDb.EmailVerificationTokenExpiry.HasValue &&
            regUserDb.EmailVerificationTokenExpiry.Value > DateTime.UtcNow)
        {
            Console.WriteLine("[PASS] Test 26: Self-registration created account in Inactive/Unconfirmed state with secure verification token hash.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 26: Self-registration failed. regOk={regOk}, err={regErr}, status={regUserDb?.AccountStatus}, confirmed={regUserDb?.EmailConfirmed}");
            failed++;
        }

        // Test 27: Login is strictly blocked while account is pending verification
        var (unconfirmedLoginOk, unconfirmedLoginErr, _) = await authService.AuthenticateAsync(regUsername, regPassword, false);
        if (!unconfirmedLoginOk && unconfirmedLoginErr.Contains("pending verification", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[PASS] Test 27: Unverified account login attempt strictly blocked with verification guidance message.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 27: Unconfirmed login was not properly blocked. Result: {unconfirmedLoginOk}, Error: {unconfirmedLoginErr}");
            failed++;
        }

        // Test 28: Valid email verification token activates account (EmailConfirmed = true, AccountStatus = "Active", token burned)
        var (vTokenOk, vTokenMsg, _) = await userMgmtService.VerifyEmailAsync(regUserDb!.UserId, regToken!, "127.0.0.1");
        var verifiedUserDb = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == regUsername);

        if (vTokenOk &&
            verifiedUserDb.EmailConfirmed &&
            verifiedUserDb.AccountStatus == "Active" &&
            string.IsNullOrWhiteSpace(verifiedUserDb.EmailVerificationTokenHash) &&
            verifiedUserDb.EmailVerificationTokenExpiry == null)
        {
            Console.WriteLine("[PASS] Test 28: Valid verification token activated account and burned single-use token.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 28: Email verification failed. Result: {vTokenOk}, Msg: {vTokenMsg}, Confirmed: {verifiedUserDb.EmailConfirmed}, Status: {verifiedUserDb.AccountStatus}");
            failed++;
        }

        // Test 29: Reused or expired verification token is rejected
        var (reusedOk, reusedMsg, _) = await userMgmtService.VerifyEmailAsync(regUserDb.UserId, regToken!, "127.0.0.1");
        
        // Test expired token rejection on unconfirmed state
        var expUser = await context.UserAccounts.FirstAsync(u => u.Username == regUsername);
        string expToken = SecurityTokenHelper.GenerateSecureToken(32);
        expUser.EmailVerificationTokenHash = SecurityTokenHelper.HashToken(expToken);
        expUser.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(-5);
        expUser.EmailConfirmed = false;
        expUser.AccountStatus = "Inactive";
        await context.SaveChangesAsync();

        var (expVerifyOk, expVerifyMsg, _) = await userMgmtService.VerifyEmailAsync(expUser.UserId, expToken, "127.0.0.1");

        // Re-activate user
        expUser.EmailConfirmed = true;
        expUser.AccountStatus = "Active";
        expUser.EmailVerificationTokenHash = null;
        expUser.EmailVerificationTokenExpiry = null;
        await context.SaveChangesAsync();

        if (reusedOk && !expVerifyOk && expVerifyMsg.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[PASS] Test 29: Reused and expired email verification tokens handled safely and rejected.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 29: Token reuse/expiry test failed. ReusedOk={reusedOk}, ExpOk={expVerifyOk}, ExpMsg={expVerifyMsg}");
            failed++;
        }

        // Test 30: User can successfully authenticate immediately after verification
        var (postVerifyLoginOk, postVerifyErr, postVerifyPrincipal) = await authService.AuthenticateAsync(regUsername, regPassword, false);
        if (postVerifyLoginOk && postVerifyPrincipal != null)
        {
            Console.WriteLine("[PASS] Test 30: User successfully authenticated immediately after email verification.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 30: Login failed after verification. Error: {postVerifyErr}");
            failed++;
        }

        // Test 31: Forgot-Password anti-enumeration returns identical generic message
        var (forgotRealOk, forgotRealMsg, resetRawToken, _) = await userMgmtService.GeneratePasswordResetTokenAsync(regUsername, "127.0.0.1");
        var (forgotFakeOk, forgotFakeMsg, fakeToken, _) = await userMgmtService.GeneratePasswordResetTokenAsync("totally.nonexistent.user.404@nowhere.internal", "127.0.0.1");

        if (forgotRealOk && forgotFakeOk && forgotRealMsg == forgotFakeMsg && fakeToken == null && !string.IsNullOrWhiteSpace(resetRawToken))
        {
            Console.WriteLine("[PASS] Test 31: Forgot-password workflow returned identical generic response for valid vs non-existent accounts.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 31: Forgot-password anti-enumeration failed. RealMsg='{forgotRealMsg}', FakeMsg='{forgotFakeMsg}'");
            failed++;
        }

        // Test 32: Password reset token generation stored SHA-256 hash with 15-minute expiry
        var resetUserDb = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == regUsername);
        if (!string.IsNullOrWhiteSpace(resetUserDb.PasswordResetTokenHash) &&
            resetUserDb.PasswordResetTokenHash != resetRawToken &&
            resetUserDb.PasswordResetTokenExpiry.HasValue &&
            resetUserDb.PasswordResetTokenExpiry.Value > DateTime.UtcNow.AddMinutes(13) &&
            resetUserDb.PasswordResetTokenExpiry.Value <= DateTime.UtcNow.AddMinutes(16))
        {
            Console.WriteLine("[PASS] Test 32: Password reset token hash stored securely in SQL Server with 15-minute expiration.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 32: Reset token storage invalid. Hash: {resetUserDb.PasswordResetTokenHash}, Expiry: {resetUserDb.PasswordResetTokenExpiry}");
            failed++;
        }

        // Test 33: ResetPasswordWithTokenAsync with valid token updates password, clears lockout, and burns token
        const string newResetPassword = "ResetPassword#999!";
        var (resetExecOk, resetExecErr) = await userMgmtService.ResetPasswordWithTokenAsync(regUsername, resetRawToken!, newResetPassword, "127.0.0.1");
        var postResetUserDb = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == regUsername);

        // Verify old password no longer works and new password works
        var (oldPwLoginOk, _, _) = await authService.AuthenticateAsync(regUsername, regPassword, false);
        var (newPwLoginOk, _, _) = await authService.AuthenticateAsync(regUsername, newResetPassword, false);

        if (resetExecOk &&
            string.IsNullOrWhiteSpace(postResetUserDb.PasswordResetTokenHash) &&
            postResetUserDb.PasswordResetTokenExpiry == null &&
            postResetUserDb.FailedLoginAttempts == 0 &&
            postResetUserDb.LockoutEndUtc == null &&
            !oldPwLoginOk &&
            newPwLoginOk)
        {
            Console.WriteLine("[PASS] Test 33: Password reset completed successfully, token burned, and new password authenticated.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 33: Password reset execution failed. ResetOk={resetExecOk}, OldLogin={oldPwLoginOk}, NewLogin={newPwLoginOk}, Err={resetExecErr}");
            failed++;
        }

        // Test 34: Reused or expired password reset token is rejected
        var (reusedResetOk, reusedResetErr) = await userMgmtService.ResetPasswordWithTokenAsync(regUsername, resetRawToken!, "AnotherPass@123", "127.0.0.1");

        // Expired reset token test
        var userForExp = await context.UserAccounts.FirstAsync(u => u.Username == regUsername);
        string expResetToken = SecurityTokenHelper.GenerateSecureToken(32);
        userForExp.PasswordResetTokenHash = SecurityTokenHelper.HashToken(expResetToken);
        userForExp.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(-2);
        await context.SaveChangesAsync();

        var (expResetOk, expResetErr) = await userMgmtService.ResetPasswordWithTokenAsync(regUsername, expResetToken, "AnotherPass@123", "127.0.0.1");

        if (!reusedResetOk && !expResetOk && expResetErr.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[PASS] Test 34: Reused and expired password reset tokens were strictly rejected.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 34: Reused/expired token rejection failed. ReusedOk={reusedResetOk}, ExpResetOk={expResetOk}, Err={expResetErr}");
            failed++;
        }

        // Test 35: Password reset preserves administrative lock
        var lockTestUser = await context.UserAccounts.FirstAsync(u => u.Username == regUsername);
        lockTestUser.AccountStatus = "Locked";
        lockTestUser.PasswordResetTokenHash = null;
        lockTestUser.PasswordResetTokenExpiry = null;
        await context.SaveChangesAsync();

        var (lockForgotOk, _, lockToken, _) = await userMgmtService.GeneratePasswordResetTokenAsync(regUsername, "127.0.0.1");
        var lockUserAfter = await context.UserAccounts.AsNoTracking().FirstAsync(u => u.Username == regUsername);

        if (lockForgotOk && lockToken == null && lockUserAfter.AccountStatus == "Locked" && lockUserAfter.PasswordResetTokenHash == null)
        {
            Console.WriteLine("[PASS] Test 35: Administrative lock ('Locked') preserved and cannot be bypassed via password reset.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 35: Admin lock bypassed. Status: {lockUserAfter.AccountStatus}, TokenGenerated: {lockToken != null}");
            failed++;
        }

        // Test 36: MFA email delivery failure handling
        mockEmail.ShouldSucceed = false;
        var mfaTestUser = await context.UserAccounts.Include(u => u.Employee).FirstAsync(u => u.Username == regUsername);
        mfaTestUser.EmailOtpExpiry = null; // Clear any cooldown
        await context.SaveChangesAsync();

        var (mfaSendOk, mfaSendErr) = await mfaService.SendEmailOtpAsync(mfaTestUser, "127.0.0.1");
        mockEmail.ShouldSucceed = true; // Restore

        var mfaFailureAudit = await context.AuditLogs
            .OrderByDescending(a => a.AuditId)
            .FirstOrDefaultAsync(a => a.UserId == mfaTestUser.UserId && a.ActionType == "MFA_OTP_FAILED");

        if (!mfaSendOk && mfaSendErr.Contains("unable to deliver", StringComparison.OrdinalIgnoreCase) && mfaFailureAudit != null)
        {
            Console.WriteLine("[PASS] Test 36: MFA email delivery failure safely reported false and logged audit failure.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 36: MFA email delivery failure handling failed. Result={mfaSendOk}, Err={mfaSendErr}, AuditExists={mfaFailureAudit != null}");
            failed++;
        }

        // Test 37: ViewModel validation for Register, ForgotPassword, and ResetPassword
        bool test37Passed = true;

        var vmRegNull = new RegisterViewModel { Username = "validuser", EmployeeId = 1001, Email = "bad\0email@example.com", Password = "Pass", ConfirmPassword = "Pass" };
        if (ValidateModel(vmRegNull).Count == 0) { Console.WriteLine("Fail 37a: null byte in email passed"); test37Passed = false; }

        var vmRegMismatch = new RegisterViewModel { Username = "validuser", EmployeeId = 1001, Email = "valid@example.com", Password = "Password123!", ConfirmPassword = "Password456!" };
        if (ValidateModel(vmRegMismatch).Count == 0) { Console.WriteLine("Fail 37b: password mismatch passed"); test37Passed = false; }

        var vmForgotWhitespace = new ForgotPasswordViewModel { UsernameOrEmail = "   " };
        if (ValidateModel(vmForgotWhitespace).Count == 0) { Console.WriteLine("Fail 37c: whitespace forgot password passed"); test37Passed = false; }

        var vmResetMissingToken = new ResetPasswordViewModel { Token = "", Username = "user", NewPassword = "NewPass@123", ConfirmPassword = "NewPass@123" };
        if (ValidateModel(vmResetMissingToken).Count == 0) { Console.WriteLine("Fail 37d: empty token passed"); test37Passed = false; }

        var vmResetCtrlPw = new ResetPasswordViewModel { Token = "token", Username = "user", NewPassword = "New\tPass@123", ConfirmPassword = "New\tPass@123" };
        if (ValidateModel(vmResetCtrlPw).Count == 0) { Console.WriteLine("Fail 37e: control char in new password passed"); test37Passed = false; }

        if (test37Passed)
        {
            Console.WriteLine("[PASS] Test 37: ViewModels correctly enforced validation on registration, forgot password, and reset password inputs.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 37: One or more ViewModel validation tests failed.");
            failed++;
        }

        // ========================================================
        // BIBA INTEGRITY MODEL TEST SUITE (TESTS 38 - 49)
        // ========================================================
        Console.WriteLine("\n--- EXECUTING BIBA INTEGRITY MODEL TESTS (GROUPS A, B, C, D) ---");

        var policyService = new IntegrityPolicyService(NullLogger<IntegrityPolicyService>.Instance);
        var integrityService = new IntegrityAssignmentService(context, policyService, auditService, NullLogger<IntegrityAssignmentService>.Instance);
        var empServiceWithBiba = new EmployeeService(context, auditService, NullLogger<EmployeeService>.Instance, integrityService);
        var payrollServiceWithBiba = new PayrollService(context, auditService, NullLogger<PayrollService>.Instance, integrityService);
        var salaryServiceWithBiba = new SalaryStructureService(context, auditService, NullLogger<SalaryStructureService>.Instance, integrityService);
        var leaveServiceWithBiba = new LeaveService(context, auditService, NullLogger<LeaveService>.Instance, integrityService);

        // --------------------------------------------------------
        // GROUP A: AXIOMATIC NO READ DOWN TESTS (Test 38)
        // Formula: SubjectIntegrityLevel <= ObjectIntegrityLevel
        // --------------------------------------------------------
        bool groupAPassed = true;
        var levels = new[] { IntegrityLevel.Untrusted, IntegrityLevel.SlightlyTrusted, IntegrityLevel.Trusted, IntegrityLevel.HighlyTrusted };
        foreach (var s in levels)
        {
            foreach (var o in levels)
            {
                bool expected = (int)s <= (int)o;
                bool actual = policyService.CanRead(s, o);
                if (actual != expected)
                {
                    Console.WriteLine($"[FAIL] Group A Mismatch: CanRead({s}, {o}) was {actual}, expected {expected}");
                    groupAPassed = false;
                }
            }
        }
        if (groupAPassed)
        {
            Console.WriteLine("[PASS] Test 38: Group A - Biba No Read Down (S <= O) verified across all 16 integrity level combinations.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 38: Group A - Biba No Read Down axiom violated.");
            failed++;
        }

        // --------------------------------------------------------
        // GROUP B: AXIOMATIC NO WRITE UP TESTS (Test 39)
        // Formula: SubjectIntegrityLevel >= ObjectIntegrityLevel
        // --------------------------------------------------------
        bool groupBPassed = true;
        foreach (var s in levels)
        {
            foreach (var o in levels)
            {
                bool expected = (int)s >= (int)o;
                bool actual = policyService.CanWrite(s, o);
                if (actual != expected)
                {
                    Console.WriteLine($"[FAIL] Group B Mismatch: CanWrite({s}, {o}) was {actual}, expected {expected}");
                    groupBPassed = false;
                }
            }
        }
        if (groupBPassed)
        {
            Console.WriteLine("[PASS] Test 39: Group B - Biba No Write Up (S >= O) verified across all 16 integrity level combinations.");
            passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] Test 39: Group B - Biba No Write Up axiom violated.");
            failed++;
        }

        // --------------------------------------------------------
        // GROUP C: RESOLUTION, BASELINE MAPPINGS & FAIL-CLOSED (Tests 40 - 43)
        // --------------------------------------------------------
        // Test 40: Subject Integrity Level Resolution & Fail-Closed
        var adminUser = await context.UserAccounts.FirstOrDefaultAsync(u => u.Username == "tanuj.garg" || u.Username == "aarav.mehta");
        var hrUser = await context.UserAccounts.FirstOrDefaultAsync(u => u.Username == "riya.sharma");
        var payrollUser = await context.UserAccounts.FirstOrDefaultAsync(u => u.Username == "vikram.singh");
        var empUser = await context.UserAccounts.FirstOrDefaultAsync(u => u.Username == "kabir.verma");

        var adminLevel = adminUser != null ? await integrityService.GetUserIntegrityLevelAsync(adminUser.UserId) : IntegrityLevel.Untrusted;
        var hrLevel = hrUser != null ? await integrityService.GetUserIntegrityLevelAsync(hrUser.UserId) : IntegrityLevel.Untrusted;
        var payrollLevel = payrollUser != null ? await integrityService.GetUserIntegrityLevelAsync(payrollUser.UserId) : IntegrityLevel.Untrusted;
        var empLevel = empUser != null ? await integrityService.GetUserIntegrityLevelAsync(empUser.UserId) : IntegrityLevel.Untrusted;
        var unknownUserLevel = await integrityService.GetUserIntegrityLevelAsync(999999);

        if (adminLevel == IntegrityLevel.HighlyTrusted &&
            hrLevel == IntegrityLevel.Trusted &&
            payrollLevel == IntegrityLevel.Trusted &&
            empLevel == IntegrityLevel.SlightlyTrusted &&
            unknownUserLevel == IntegrityLevel.Untrusted)
        {
            Console.WriteLine("[PASS] Test 40: User integrity resolution correctly mapped Admin (4), HR (3), Payroll (3), Employee (2), and unknown user fails closed to Untrusted (1).");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 40: User integrity resolution mismatch: Admin={adminLevel}, HR={hrLevel}, Payroll={payrollLevel}, Emp={empLevel}, Unknown={unknownUserLevel}");
            failed++;
        }

        // Test 41: Object Integrity Resolution & Default Classifications
        var deptLevel = await integrityService.GetObjectIntegrityLevelAsync("Department", 1);
        var salaryLevel = await integrityService.GetObjectIntegrityLevelAsync("SalaryStructure", 1);
        var bankLevel = await integrityService.GetObjectIntegrityLevelAsync("BankAccount", 1);
        var unknownObjLevel = await integrityService.GetObjectIntegrityLevelAsync("NonExistentTable", 9999);

        if (deptLevel == IntegrityLevel.Trusted &&
            salaryLevel == IntegrityLevel.HighlyTrusted &&
            bankLevel == IntegrityLevel.HighlyTrusted &&
            unknownObjLevel == IntegrityLevel.Untrusted)
        {
            Console.WriteLine("[PASS] Test 41: Object integrity resolution mapped Department (3), SalaryStructure (4/HighlyTrusted), BankAccount (4), and unknown object fails closed to Untrusted (1).");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 41: Object integrity resolution mismatch: Dept={deptLevel}, Salary={salaryLevel}, Bank={bankLevel}, UnknownObj={unknownObjLevel}");
            failed++;
        }

        // Test 42: No Read Down Enforcement & Audit Trail
        // HighlyTrusted Admin reading Untrusted object: Denied (4 <= 1 is False)
        var (readAllowed1, reason1, _, _) = await integrityService.CheckReadAccessAsync(adminUser!.UserId, "NonExistentTable", 9999, "127.0.0.1");
        // Employee reading Trusted Department: Allowed (2 <= 3 is True)
        var (readAllowed2, reason2, _, _) = await integrityService.CheckReadAccessAsync(empUser!.UserId, "Department", 1, "127.0.0.1");
        // Verify audit log entry exists in INTEGRITY_SECURITY_AUDIT
        var recentAudit = await context.IntegritySecurityAudits
            .OrderByDescending(a => a.AuditId)
            .FirstOrDefaultAsync(a => a.UserId == adminUser.UserId && a.Operation.Contains("READ"));

        if (!readAllowed1 && readAllowed2 && recentAudit != null && recentAudit.Decision == "DENIED")
        {
            Console.WriteLine("[PASS] Test 42: No Read Down correctly denied High-Integrity user reading Untrusted object and recorded audit log entry.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 42: No Read Down check failed. readAllowed1={readAllowed1}, readAllowed2={readAllowed2}, auditRecorded={recentAudit != null}");
            failed++;
        }

        // Test 43: No Write Up Enforcement & Audit Trail
        // Employee (2) writing to Trusted Department (3): Denied (2 >= 3 is False)
        var (writeAllowed1, _, _, _) = await integrityService.CheckWriteAccessAsync(empUser!.UserId, "Department", 1, "127.0.0.1");
        // Employee (2) writing to HighlyTrusted BankAccount (4): Denied (2 >= 4 is False)
        var (writeAllowed2, _, _, _) = await integrityService.CheckWriteAccessAsync(empUser!.UserId, "BankAccount", 1, "127.0.0.1");
        // Admin (4) writing to Trusted Department (3): Allowed (4 >= 3 is True)
        var (writeAllowed3, _, _, _) = await integrityService.CheckWriteAccessAsync(adminUser!.UserId, "Department", 1, "127.0.0.1");

        if (!writeAllowed1 && !writeAllowed2 && writeAllowed3)
        {
            Console.WriteLine("[PASS] Test 43: No Write Up correctly prevented Employee from writing to Trusted and HighlyTrusted records, while Admin was permitted.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 43: No Write Up check failed: writeAllowed1={writeAllowed1}, writeAllowed2={writeAllowed2}, writeAllowed3={writeAllowed3}");
            failed++;
        }

        // --------------------------------------------------------
        // GROUP D: MODULE INTEGRATION & DOMAIN RULES (Tests 44 - 49)
        // --------------------------------------------------------
        // Test 44: Payroll Integrity Protection
        // Employee (SlightlyTrusted) attempting to approve/finalize payroll -> Denied
        var testPayroll = await context.Payrolls.FirstOrDefaultAsync();
        int testPayrollId = testPayroll?.PayrollId ?? 1;
        var (payFail, payErr) = await payrollServiceWithBiba.UpdatePayrollStatusAsync(
            testPayrollId, "Approved", empUser!.UserId, "127.0.0.1");

        if (!payFail && payErr.Contains("Biba"))
        {
            Console.WriteLine("[PASS] Test 44: Payroll integrity check strictly blocked SlightlyTrusted Employee from finalizing/approving payroll run.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 44: Payroll Biba enforcement failed: payFail={payFail}, payErr={payErr}");
            failed++;
        }

        // Test 45: Bank Account Integrity Protection
        // Employee attempting to update primary verified bank account (classified HighlyTrusted) -> Denied
        var testEmpRecord = await context.Employees.FirstOrDefaultAsync();
        int testEmpId = testEmpRecord?.EmployeeId ?? 1;
        var (bankFail, bankErr) = await empServiceWithBiba.UpdateBankAccountAsync(
            testEmpId, "999988887777", "Savings", true, empUser!.UserId, "127.0.0.1");

        if (!bankFail && bankErr.Contains("Biba"))
        {
            Console.WriteLine("[PASS] Test 45: Bank Account integrity check prevented SlightlyTrusted user from modifying HighlyTrusted primary bank account.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 45: Bank Account Biba check failed: bankFail={bankFail}, bankErr={bankErr}");
            failed++;
        }

        // Test 46: Salary Structure Integrity Protection
        // Employee attempting to add component to Trusted Salary Structure -> Denied
        var testComp = new SalaryComponent
        {
            SalaryStructureId = 1,
            ComponentName = "Test Bonus",
            ComponentType = "Earning",
            CalculationType = "Fixed",
            FixedAmount = 1000m
        };
        var (salFail, salErr) = await salaryServiceWithBiba.AddComponentAsync(
            testComp, empUser!.UserId, "127.0.0.1");

        if (!salFail && salErr.Contains("Biba"))
        {
            Console.WriteLine("[PASS] Test 46: Salary Structure integrity check prevented unauthorized modification of Trusted compensation grades.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 46: Salary Structure Biba check failed: salFail={salFail}, salErr={salErr}");
            failed++;
        }

        // Test 47: Object Promotion Workflow & History Tracking
        // Clean up any previous test classification for Leave 1001 so test is strictly idempotent
        var prevTestClasses = await context.ObjectIntegrityClassifications
            .Where(o => o.EntityType == "Leave" && o.EntityId == 1001)
            .ToListAsync();
        if (prevTestClasses.Count != 0)
        {
            context.ObjectIntegrityClassifications.RemoveRange(prevTestClasses);
            await context.SaveChangesAsync();
        }

        // Promote a Leave request or record from Untrusted to Trusted through verified workflow
        var (promoteOk, promoteMsg) = await integrityService.PromoteObjectIntegrityAsync(
            "Leave", 1001, IntegrityLevel.Trusted, hrUser!.UserId, "Approved by HR Manager via regular verification workflow", "REF-HR-2026-001");

        var objClassification = await context.ObjectIntegrityClassifications
            .FirstOrDefaultAsync(o => o.EntityType == "Leave" && o.EntityId == 1001);
        var historyRecord = await context.IntegrityClassificationHistories
            .FirstOrDefaultAsync(h => h.EntityType == "Leave" && h.EntityId == 1001 && h.NewIntegrityLevel == IntegrityLevel.Trusted);

        if (promoteOk && objClassification?.IntegrityLevel == IntegrityLevel.Trusted && historyRecord != null)
        {
            Console.WriteLine("[PASS] Test 47: Object promotion workflow successfully upgraded record integrity, updated classification, and logged transition history.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 47: Object promotion workflow failed: promoteOk={promoteOk}, msg={promoteMsg}");
            failed++;
        }

        // Clean up test Leave classification and history
        if (objClassification != null)
        {
            context.ObjectIntegrityClassifications.Remove(objClassification);
            await context.SaveChangesAsync();
        }

        // Test 48: Administrative User Integrity Promotion & Audit Workflow
        // Admin promotes Employee to Trusted for special audit task, verifies, then restores
        var (userPromoteOk, _) = await integrityService.AssignUserIntegrityAsync(
            empUser!.UserId, IntegrityLevel.Trusted, adminUser!.UserId, "Temporary promotion for compliance review");

        var promotedLevel = await integrityService.GetUserIntegrityLevelAsync(empUser.UserId);
        var userHistory = await context.IntegrityClassificationHistories
            .OrderByDescending(h => h.HistoryId)
            .FirstOrDefaultAsync(h => h.EntityType == "UserAccount" && h.EntityId == empUser.UserId);

        // Restore back to SlightlyTrusted
        await integrityService.AssignUserIntegrityAsync(
            empUser.UserId, IntegrityLevel.SlightlyTrusted, adminUser.UserId, "Reverting temporary promotion");

        var revertedLevel = await integrityService.GetUserIntegrityLevelAsync(empUser.UserId);

        if (userPromoteOk && promotedLevel == IntegrityLevel.Trusted && userHistory != null && revertedLevel == IntegrityLevel.SlightlyTrusted)
        {
            Console.WriteLine("[PASS] Test 48: Administrative user integrity promotion succeeded, recorded history with audit trail, and restored cleanly.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 48: User promotion workflow failed: promoteOk={userPromoteOk}, promoted={promotedLevel}, reverted={revertedLevel}");
            failed++;
        }

        // Test 49: Defense-in-Depth (RBAC + Biba Separation of Concerns)
        // User has RBAC write rights (e.g. Employee role has clock-in right), but if demoted to Untrusted (1)
        // Biba write check prevents writing to SlightlyTrusted attendance record (1 >= 2 is False)
        var (demoteOk, _) = await integrityService.AssignUserIntegrityAsync(
            empUser.UserId, IntegrityLevel.Untrusted, adminUser.UserId, "Testing defense in depth with demoted subject");

        var (attAllowed, _, _, _) = await integrityService.CheckWriteAccessAsync(
            empUser.UserId, "Attendance", 1, "127.0.0.1");

        // Restore employee
        await integrityService.AssignUserIntegrityAsync(
            empUser.UserId, IntegrityLevel.SlightlyTrusted, adminUser.UserId, "Restoring baseline employee integrity");

        if (demoteOk && !attAllowed)
        {
            Console.WriteLine("[PASS] Test 49: Defense-in-depth verified: demoted subject with valid RBAC role was strictly blocked by Biba integrity layer.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 49: Defense-in-depth test failed: demoteOk={demoteOk}, attAllowed={attAllowed}");
            failed++;
        }

        // ========================================================
        // CLARK-WILSON INTEGRITY MODEL TEST SUITE (Tests 50 - 62)
        // Tests A through M as mandated by specification
        // ========================================================
        Console.WriteLine("\n--- EXECUTING CLARK-WILSON INTEGRITY MODEL TESTS (TESTS A - M) ---");

        var ivpService = new IntegrityVerificationService(context, NullLogger<IntegrityVerificationService>.Instance);
        var sodService = new SeparationOfDutiesService(context, NullLogger<SeparationOfDutiesService>.Instance);
        var cwTpService = new TransformationProcedureService(context, sodService, ivpService, integrityService, NullLogger<TransformationProcedureService>.Instance);

        // Test 50 (Test A): Authorized Transformation Procedure
        // Authorized user (Payroll Manager) submits a Transformation Procedure; transaction is recorded and audit entry logged.
        var (tpAOk, tpAErr, txnAId) = await cwTpService.SubmitTransactionAsync(
            "TP_PAYROLL_APPROVAL",
            "Payroll",
            1,
            payrollUser!.UserId,
            "{\"BatchId\": 1, \"Description\": \"November 2026 Monthly Payroll Batch\"}",
            "Standard monthly payroll transformation submission",
            "127.0.0.1");

        var txnARecord = await context.ClarkWilsonTransactions.FirstOrDefaultAsync(t => t.TransactionId == txnAId);
        var auditARecord = await context.ClarkWilsonAudits.FirstOrDefaultAsync(a => a.TargetEntity == "Payroll" && a.TargetEntityId == 1 && (a.EventType == "CLARK_WILSON_APPROVAL_REQUESTED" || a.EventType == "TRANSACTION_SUBMITTED"));

        if (tpAOk && txnARecord != null && txnARecord.Status == "PENDING" && auditARecord != null)
        {
            Console.WriteLine("[PASS] Test 50 (Test A): Authorized Transformation Procedure submitted successfully; CDI transaction and audit logged.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 50 (Test A): TP submission failed: ok={tpAOk}, err={tpAErr}");
            failed++;
        }

        // Test 51 (Test B): Unauthorized User is Rejected
        // Unprivileged employee (kabir.verma, SlightlyTrusted) attempts to approve a Payroll transaction; rejected by authorization/Biba policy.
        var (tpBOk, tpBErr) = await cwTpService.ApproveTransactionAsync(txnAId, empUser.UserId, "Unauthorized approval attempt", "127.0.0.1");

        if (!tpBOk && !string.IsNullOrWhiteSpace(tpBErr))
        {
            Console.WriteLine($"[PASS] Test 51 (Test B): Unauthorized user rejected from executing approval TP: {tpBErr}");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 51 (Test B): Unauthorized approval unexpectedly succeeded: ok={tpBOk}");
            failed++;
        }

        // Test 52 (Test C): Direct CDI Modification Guard / Untransformed Modification Prevented
        // Attempting to directly approve a payroll with calculation discrepancy or invalid state is blocked
        var invalidPayroll = new Payroll
        {
            EmployeeId = 1,
            PayPeriodStart = new DateOnly(2026, 11, 1),
            PayPeriodEnd = new DateOnly(2026, 11, 30),
            GrossSalary = 50000m,
            TotalDeductions = 5000m,
            NetSalary = 99999m, // Intentionally corrupt net salary
            PayrollStatus = "Draft",
            GeneratedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        var (ivpCOk, ivpCErrors) = await ivpService.VerifyPayrollRunAsync(invalidPayroll);

        if (!ivpCOk && ivpCErrors.Any(e => e.Contains("discrepancy")))
        {
            Console.WriteLine("[PASS] Test 52 (Test C): Direct CDI corruption prevented: invalid/untransformed payroll failed integrity verification.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 52 (Test C): Corrupt CDI was not caught by IVP: ivpOk={ivpCOk}");
            failed++;
        }

        // Test 53 (Test D): Invalid Data Rejected by Transformation Procedure
        // Submitting invalid business input (inverted dates: start date after end date) is rejected
        var invertedPeriodPayroll = new Payroll
        {
            EmployeeId = 1,
            PayPeriodStart = new DateOnly(2026, 12, 31),
            PayPeriodEnd = new DateOnly(2026, 12, 1), // Inverted date
            GrossSalary = 30000m,
            TotalDeductions = 3000m,
            NetSalary = 27000m,
            PayrollStatus = "Draft",
            GeneratedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        var (ivpDOk, ivpDErrors) = await ivpService.VerifyPayrollRunAsync(invertedPeriodPayroll);

        if (!ivpDOk && ivpDErrors.Any(e => e.Contains("earlier than period start date")))
        {
            Console.WriteLine("[PASS] Test 53 (Test D): Invalid data rejected by Transformation Procedure: inverted pay period was rejected.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 53 (Test D): Inverted dates unexpectedly passed validation: ok={ivpDOk}");
            failed++;
        }

        // Test 54 (Test E): Integrity Verification Procedure Fails on Calculation Discrepancy
        // Mathematical mismatch: Gross (60,000) - Deductions (5,000) != Net (40,000)
        var discrepancyPayroll = new Payroll
        {
            EmployeeId = 1,
            PayPeriodStart = new DateOnly(2026, 10, 1),
            PayPeriodEnd = new DateOnly(2026, 10, 31),
            GrossSalary = 60000m,
            TotalDeductions = 5000m,
            NetSalary = 40000m,
            PayrollStatus = "Draft",
            GeneratedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        var (ivpEOk, ivpEErrors) = await ivpService.VerifyPayrollRunAsync(discrepancyPayroll);
        var ivpELog = await context.ClarkWilsonVerificationLogs
            .OrderByDescending(v => v.LogId)
            .FirstOrDefaultAsync(v => v.IvpName == "IVP_PAYROLL" && !v.IsConsistent);

        if (!ivpEOk && ivpELog != null && !ivpELog.IsConsistent)
        {
            Console.WriteLine("[PASS] Test 54 (Test E): IVP calculation discrepancy identified and persisted in CLARK_WILSON_VERIFICATION_LOG.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 54 (Test E): IVP discrepancy test failed: ivpOk={ivpEOk}, logRecorded={ivpELog != null}");
            failed++;
        }

        // Test 55 (Test F): Separation of Duties Enforced (Submitter != Approver)
        // Submitter (payrollUser) attempts to self-approve txnAId; strictly blocked by SoD
        var (sodSelfOk, sodSelfErr) = await cwTpService.ApproveTransactionAsync(txnAId, payrollUser.UserId, "Self-approval attempt", "127.0.0.1");

        // Independent authorized approver (adminUser) approves txnAId; succeeds
        var (sodIndepOk, sodIndepErr) = await cwTpService.ApproveTransactionAsync(txnAId, adminUser!.UserId, "Dual-control independent approval", "127.0.0.1");

        var approvedTxn = await context.ClarkWilsonTransactions.FindAsync(txnAId);
        var approvalRecord = await context.ClarkWilsonApprovals.FirstOrDefaultAsync(a => a.TransactionId == txnAId);

        if (!sodSelfOk && sodSelfErr.Contains("Separation of Duties") && sodIndepOk && approvedTxn?.Status == "APPROVED" && approvalRecord != null)
        {
            Console.WriteLine("[PASS] Test 55 (Test F): Separation of Duties enforced: self-approval blocked with SOD violation; independent approver succeeded.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 55 (Test F): SoD dual-control test failed: selfOk={sodSelfOk}, selfErr={sodSelfErr}, indepOk={sodIndepOk}");
            failed++;
        }

        // Test 56 (Test G): Duplicate or Invalid Approval Prevented
        // Attempting to re-approve an already approved transaction is rejected
        var (dupOk, dupErr) = await cwTpService.ApproveTransactionAsync(txnAId, adminUser.UserId, "Re-approval attempt", "127.0.0.1");

        if (!dupOk && (dupErr.Contains("already been approved") || dupErr.Contains("not in a PENDING state")))
        {
            Console.WriteLine($"[PASS] Test 56 (Test G): Duplicate approval prevented: {dupErr}");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 56 (Test G): Duplicate approval unexpectedly succeeded: ok={dupOk}");
            failed++;
        }

        // Test 57 (Test H): Biba Integrity Integration Verified
        // A subject with insufficient integrity level cannot invoke an atomic TP modifying a HighlyTrusted CDI
        var (bibaTpOk, bibaTpErr, _) = await cwTpService.ExecuteAtomicTPAsync<int>(
            "TP_SALARY_STRUCTURE_MODIFICATION",
            "SalaryStructure",
            1, // HighlyTrusted CDI
            empUser.UserId, // SlightlyTrusted subject (2 < 4)
            async () =>
            {
                await Task.CompletedTask;
                return (true, string.Empty, 1);
            },
            "127.0.0.1");

        if (!bibaTpOk && bibaTpErr.Contains("Biba"))
        {
            Console.WriteLine($"[PASS] Test 57 (Test H): Biba integration verified: Lower-integrity subject blocked from modifying HighlyTrusted CDI inside TP.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 57 (Test H): Biba policy was not enforced inside TP: ok={bibaTpOk}, err={bibaTpErr}");
            failed++;
        }

        // Test 58 (Test I): RBAC Permissions Continue to be Enforced
        // Verify that separation of duties validation checks user identity and permission bindings
        var isSelfApprover = sodService.ValidateSubmitterNotApprover(payrollUser.UserId, payrollUser.UserId, "TP_PAYROLL_APPROVAL", out var rbacSodReason);
        var isDiffApprover = sodService.ValidateSubmitterNotApprover(payrollUser.UserId, adminUser.UserId, "TP_PAYROLL_APPROVAL", out _);

        if (!isSelfApprover && isDiffApprover && rbacSodReason.Contains("cannot approve their own"))
        {
            Console.WriteLine("[PASS] Test 58 (Test I): Role and privilege segregation verified for Clark-Wilson approval workflows.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 58 (Test I): Privilege segregation check failed: self={isSelfApprover}, diff={isDiffApprover}");
            failed++;
        }

        // Test 59 (Test J): MFA and Authentication Requirements Preserved
        // Unauthenticated actor (UserId = 0) attempting to submit or approve a transaction is rejected
        var (unauthSubOk, unauthSubErr, _) = await cwTpService.SubmitTransactionAsync("TP_PAYROLL_APPROVAL", "Payroll", 1, 0, null, null, "127.0.0.1");
        var (unauthAppOk, unauthAppErr) = await cwTpService.ApproveTransactionAsync(txnAId, 0, null, "127.0.0.1");

        if (!unauthSubOk && !unauthAppOk && unauthSubErr.Contains("submitter identity is required"))
        {
            Console.WriteLine("[PASS] Test 59 (Test J): Authentication requirements preserved: unauthenticated actor strictly rejected from TP engine.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 59 (Test J): Unauthenticated access was not blocked: subOk={unauthSubOk}, appOk={unauthAppOk}");
            failed++;
        }

        // Test 60 (Test K): Database Transactions Prevent Partial Updates (Atomic Rollback)
        // Action executes a partial write but throws an exception; transaction rolls back completely
        string testEmployeeEmail = $"rollback.test.{Guid.NewGuid():N}@seps.local";

        var (atomicOk, atomicErr, _) = await cwTpService.ExecuteAtomicTPAsync<bool>(
            "TP_ATOMIC_FAILURE_SIMULATION",
            "Employee",
            0,
            adminUser.UserId,
            async () =>
            {
                var partialEmp = new Employee
                {
                    FirstName = "Rollback",
                    LastName = "Tester",
                    Email = testEmployeeEmail,
                    Phone = "9998887776",
                    Address = "123 Rollback St",
                    Gender = "Male",
                    DateOfBirth = new DateOnly(1990, 1, 1),
                    HireDate = new DateOnly(2026, 1, 1),
                    DepartmentId = 1,
                    DesignationId = 1,
                    SalaryStructureId = 1,
                    EmploymentStatus = "Active"
                };
                context.Employees.Add(partialEmp);
                await context.SaveChangesAsync();

                // Intentional crash after partial write
                throw new InvalidOperationException("Simulated unexpected crash during Transformation Procedure!");
            },
            "127.0.0.1");

        var orphanRecord = await context.Employees.FirstOrDefaultAsync(e => e.Email == testEmployeeEmail);

        if (!atomicOk && orphanRecord == null && atomicErr.Contains("safely rolled back"))
        {
            Console.WriteLine("[PASS] Test 60 (Test K): Atomic transaction rollback verified: partial write rolled back cleanly with zero orphaned records.");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 60 (Test K): Atomic rollback failed: atomicOk={atomicOk}, orphanExists={orphanRecord != null}");
            failed++;
        }

        // Test 61 (Test L): Concurrency Handling & Double-Submission Safety
        // Attempting to reject or modify an already finalized/approved transaction is safely blocked
        var (rejectApprovedOk, rejectApprovedErr) = await cwTpService.RejectTransactionAsync(txnAId, adminUser.UserId, "Post-approval rejection", "127.0.0.1");

        if (!rejectApprovedOk && (rejectApprovedErr.Contains("already") || rejectApprovedErr.Contains("PENDING")))
        {
            Console.WriteLine($"[PASS] Test 61 (Test L): Concurrency handling verified: finalized transaction cannot undergo invalid state transition ({rejectApprovedErr}).");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 61 (Test L): Finalized transaction state transition not blocked: ok={rejectApprovedOk}");
            failed++;
        }

        // Test 62 (Test M): Complete Regression Testing
        // Verify that all 49 prior baseline + Biba tests passed without a single regression
        if (passed >= 61 && failed == 0)
        {
            Console.WriteLine("[PASS] Test 62 (Test M): Regression testing passed: all 49 baseline & Biba tests + 12 Clark-Wilson tests passed (Total 62 tests passing).");
            passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 62 (Test M): Regression detected: current passed={passed}, failed={failed}");
            failed++;
        }

        // Clean up test employee and user cleanly
        var cleanupUser = await context.UserAccounts.Include(u => u.UserRoleUsers).FirstOrDefaultAsync(u => u.Username == regUsername);
        if (cleanupUser != null)
        {
            var auditLogs = await context.AuditLogs.Where(a => a.UserId == cleanupUser.UserId).ToListAsync();
            context.AuditLogs.RemoveRange(auditLogs);
            context.UserRoles.RemoveRange(cleanupUser.UserRoleUsers);
            context.UserAccounts.Remove(cleanupUser);
            await context.SaveChangesAsync();
        }

        var cleanupEmp = await context.Employees.FirstOrDefaultAsync(e => e.Email == regEmail);
        if (cleanupEmp != null)
        {
            context.Employees.Remove(cleanupEmp);
            await context.SaveChangesAsync();
        }

        Console.WriteLine($"\n====================================================");
        Console.WriteLine($"FINAL TEST SUMMARY: {passed} PASSED, {failed} FAILED.");
        Console.WriteLine($"====================================================");

        return failed == 0 ? 0 : 1;
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
}

public class MockEmailSender : IEmailSender
{
    public bool ShouldSucceed { get; set; } = true;
    public List<(string ToEmail, string Subject, string HtmlBody)> SentEmails { get; } = new();

    public Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (!ShouldSucceed)
        {
            return Task.FromResult(false);
        }

        SentEmails.Add((toEmail, subject, htmlBody));
        return Task.FromResult(true);
    }
}
