using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class UserManagementService
{
    private readonly PayrollDbContext _context;
    private readonly AuditService _audit;
    private readonly PasswordHasher<UserAccount> _hasher;
    private readonly ILogger<UserManagementService> _logger;
    private readonly IEmailSender? _emailSender;
    private readonly IConfiguration? _config;

    public UserManagementService(
        PayrollDbContext context,
        AuditService audit,
        ILogger<UserManagementService> logger,
        IEmailSender? emailSender = null,
        IConfiguration? config = null)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
        _emailSender = emailSender;
        _config = config;
        _hasher = new PasswordHasher<UserAccount>();
    }

    public async Task<(List<UserAccount> Items, int TotalCount)> GetUsersAsync(
        string? search,
        string? status,
        int? roleId,
        int page = 1,
        int pageSize = 15)
    {
        var query = _context.UserAccounts
            .Include(u => u.Employee)
                .ThenInclude(e => e.Department)
            .Include(u => u.Employee.Designation)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(u =>
                u.Username.Contains(search) ||
                (u.Employee != null && (u.Employee.FirstName.Contains(search) || u.Employee.LastName.Contains(search) || u.Employee.Email.Contains(search))));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(u => u.AccountStatus == status);
        }

        if (roleId.HasValue && roleId.Value > 0)
        {
            query = query.Where(u => u.UserRoleUsers.Any(ur => ur.RoleId == roleId.Value));
        }

        int totalCount = await query.CountAsync();
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 5, 100);

        var items = await query
            .OrderBy(u => u.Username)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<UserAccount?> GetUserByIdAsync(int id)
    {
        return await _context.UserAccounts
            .Include(u => u.Employee)
                .ThenInclude(e => e.Department)
            .Include(u => u.Employee.Designation)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.AuditLogs.OrderByDescending(a => a.ActionDateTime).Take(15))
            .FirstOrDefaultAsync(u => u.UserId == id);
    }

    public async Task<(bool Success, string Error)> UnlockAccountAsync(int userId, int currentUserId, string? ipAddress)
    {
        try
        {
            var user = await _context.UserAccounts.FindAsync(userId);
            if (user == null) return (false, "User not found.");

            user.AccountStatus = "Active";
            user.EmailConfirmed = true;
            user.EmailVerificationTokenHash = null;
            user.EmailVerificationTokenExpiry = null;
            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "UNLOCK", "USER_ACCOUNT", user.UserId, "Success",
                $"Unlocked user account {user.Username}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock user {Id}", userId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> ToggleStatusAsync(int userId, int currentUserId, string? ipAddress)
    {
        try
        {
            var user = await _context.UserAccounts.FindAsync(userId);
            if (user == null) return (false, "User not found.");

            if (user.UserId == currentUserId)
            {
                return (false, "You cannot deactivate your own account.");
            }

            string oldStatus = user.AccountStatus;
            user.AccountStatus = oldStatus == "Active" ? "Inactive" : "Active";
            if (user.AccountStatus == "Active")
            {
                user.EmailConfirmed = true;
                user.EmailVerificationTokenHash = null;
                user.EmailVerificationTokenExpiry = null;
                user.FailedLoginAttempts = 0;
                user.LockoutEndUtc = null;
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "STATUS_CHANGE", "USER_ACCOUNT", user.UserId, "Success",
                $"Changed status of user {user.Username} from {oldStatus} to {user.AccountStatus}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle status for user {Id}", userId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> AssignRoleAsync(int userId, int roleId, int currentUserId, string? ipAddress)
    {
        try
        {
            var user = await _context.UserAccounts.FindAsync(userId);
            if (user == null) return (false, "User not found.");

            var role = await _context.AppRoles.FindAsync(roleId);
            if (role == null) return (false, "Role not found.");

            bool hasRole = await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            if (hasRole)
            {
                return (false, "User already has this role.");
            }

            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = currentUserId
            };

            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "ASSIGN_ROLE", "USER_ROLE", userId, "Success",
                $"Assigned role {role.RoleName} to user {user.Username}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign role.");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> RemoveRoleAsync(int userId, int roleId, int currentUserId, string? ipAddress)
    {
        try
        {
            var userRole = await _context.UserRoles.Include(ur => ur.Role).FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            if (userRole == null) return (false, "User does not have this role.");

            // Prevent removing the last Admin
            if (userRole.RoleId == 1) // Admin
            {
                int adminCount = await _context.UserRoles.CountAsync(ur => ur.RoleId == 1);
                if (adminCount <= 1)
                {
                    return (false, "Cannot remove the only remaining Administrator from the system.");
                }
            }

            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "REMOVE_ROLE", "USER_ROLE", userId, "Success",
                $"Removed role {userRole.Role.RoleName} from User ID {userId}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove role.");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> ResetPasswordAsync(int userId, string newPassword, int currentUserId, string? ipAddress)
    {
        try
        {
            var user = await _context.UserAccounts.FindAsync(userId);
            if (user == null) return (false, "User not found.");

            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            user.FailedLoginAttempts = 0;
            user.AccountStatus = "Active";

            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, "PASSWORD_RESET", "USER_ACCOUNT", user.UserId, "Success",
                $"Admin reset password for user {user.Username}", ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset password for user {Id}", userId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error, string? VerificationToken)> RegisterEmployeeAccountAsync(
        int employeeId,
        string email,
        string username,
        string password,
        string? ipAddress,
        string? baseUrl = null)
    {
        try
        {
            // Step 1: Verify employee exists with matching email
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.Email.ToLower() == email.Trim().ToLower());

            if (employee == null)
            {
                return (false, "Employee ID and registered email do not match any existing employee record.", null);
            }

            // Step 2: Check employee already has an account
            bool accountExists = await _context.UserAccounts.AnyAsync(u => u.EmployeeId == employeeId);
            if (accountExists)
            {
                return (false, "A user account is already registered for this employee. Please sign in or use password recovery.", null);
            }

            // Step 3: Check username uniqueness
            bool usernameTaken = await _context.UserAccounts.AnyAsync(u => u.Username.ToLower() == username.Trim().ToLower());
            if (usernameTaken)
            {
                return (false, "This username is already taken. Please choose another username.", null);
            }

            // Step 4: Generate cryptographically random single-use verification token
            string rawToken = SecurityTokenHelper.GenerateSecureToken(32);
            string tokenHash = SecurityTokenHelper.HashToken(rawToken);

            // Step 5: Create UserAccount (Pending email verification)
            var user = new UserAccount
            {
                EmployeeId = employeeId,
                Username = username.Trim(),
                AccountStatus = "Inactive", // Inactive until verified
                EmailConfirmed = false,
                EmailVerificationTokenHash = tokenHash,
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
                FailedLoginAttempts = 0,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = _hasher.HashPassword(user, password);

            _context.UserAccounts.Add(user);
            await _context.SaveChangesAsync();

            // Step 6: Assign strictly the Employee role (Role_ID = 4)
            var empRole = await _context.AppRoles.FirstOrDefaultAsync(r => r.RoleName == "Employee");
            int roleId = empRole?.RoleId ?? 4;

            var userRole = new UserRole
            {
                UserId = user.UserId,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow
            };
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            // Step 7: Send verification email if email sender is available
            string effectiveBaseUrl = (!string.IsNullOrWhiteSpace(baseUrl) ? baseUrl : (_config?["Application:BaseUrl"] ?? "http://localhost:5039")).TrimEnd('/');
            string verificationUrl = $"{effectiveBaseUrl}/Account/VerifyEmail?userId={user.UserId}&token={rawToken}";
            string emailSubject = "Verify Your SEPS Account";
            string emailBody = EmailTemplateService.BuildVerificationEmail(user.Username, verificationUrl, 24);

            if (_emailSender != null)
            {
                bool emailSent = await _emailSender.SendEmailAsync(employee.Email, emailSubject, emailBody);
                if (!emailSent)
                {
                    _logger.LogWarning("Verification email could not be delivered to {Email} during registration.", employee.Email);
                }
            }

            await _audit.LogAsync(
                user.UserId,
                "REGISTER",
                "USER_ACCOUNT",
                user.UserId,
                "Success",
                $"Employee {employee.FirstName} {employee.LastName} registered user account '{username}' (Pending verification).",
                ipAddress);

            return (true, string.Empty, rawToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to self-register employee {EmpId}", employeeId);
            return (false, "Registration error: " + ex.Message, null);
        }
    }

    public async Task<(bool Success, string Message, string? Username)> VerifyEmailAsync(int userId, string token, string? ipAddress)
    {
        try
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(token))
            {
                return (false, "Invalid verification request parameters.", null);
            }

            var user = await _context.UserAccounts
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return (false, "Account not found for verification.", null);
            }

            if (user.EmailConfirmed && string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (true, "Your email is already verified. You may sign in to your account.", user.Username);
            }

            if (string.IsNullOrWhiteSpace(user.EmailVerificationTokenHash) ||
                !user.EmailVerificationTokenExpiry.HasValue)
            {
                return (false, "No pending email verification request exists for this account.", user.Username);
            }

            if (user.EmailVerificationTokenExpiry.Value < DateTime.UtcNow)
            {
                return (false, "This email verification link has expired. Please contact your system administrator.", user.Username);
            }

            if (!SecurityTokenHelper.VerifyToken(token, user.EmailVerificationTokenHash))
            {
                return (false, "Invalid or corrupted verification token.", user.Username);
            }

            // Consume token and activate account
            user.EmailConfirmed = true;
            user.AccountStatus = "Active";
            user.EmailVerificationTokenHash = null;
            user.EmailVerificationTokenExpiry = null;
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                user.UserId,
                "EMAIL_VERIFIED",
                "USER_ACCOUNT",
                user.UserId,
                "Success",
                $"User {user.Username} verified email ownership.",
                ipAddress);

            return (true, "Your email has been verified and your account is now active! You may proceed to sign in.", user.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email for user {UserId}", userId);
            return (false, "An error occurred while verifying your email.", null);
        }
    }

    public async Task<(bool Success, string Message, string? RawToken, UserAccount? User)> GeneratePasswordResetTokenAsync(string usernameOrEmail, string? ipAddress, string? baseUrl = null)
    {
        try
        {
            const string genericMessage = "If an account matching the provided identifier exists, password reset instructions have been dispatched to the registered email address.";

            if (string.IsNullOrWhiteSpace(usernameOrEmail))
            {
                return (true, genericMessage, null, null);
            }

            string normalized = usernameOrEmail.Trim();
            var user = await _context.UserAccounts
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Username == normalized || (u.Employee != null && u.Employee.Email == normalized));

            if (user == null || user.Employee == null || string.IsNullOrWhiteSpace(user.Employee.Email))
            {
                // Prevent account enumeration by returning generic success message
                return (true, genericMessage, null, null);
            }

            if (string.Equals(user.AccountStatus, "Locked", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Password reset requested for administratively locked user {Username}.", user.Username);
                return (true, genericMessage, null, null);
            }

            string rawToken = SecurityTokenHelper.GenerateSecureToken(32);
            string tokenHash = SecurityTokenHelper.HashToken(rawToken);

            user.PasswordResetTokenHash = tokenHash;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            string effectiveBaseUrl = (!string.IsNullOrWhiteSpace(baseUrl) ? baseUrl : (_config?["Application:BaseUrl"] ?? "http://localhost:5039")).TrimEnd('/');
            string resetUrl = $"{effectiveBaseUrl}/Account/ResetPassword?token={rawToken}&username={Uri.EscapeDataString(user.Username)}";
            string emailSubject = "Reset Your SEPS Password";
            string emailBody = EmailTemplateService.BuildPasswordResetEmail(user.Username, resetUrl, 15);

            if (_emailSender != null)
            {
                bool sent = await _emailSender.SendEmailAsync(user.Employee.Email, emailSubject, emailBody);
                if (!sent)
                {
                    _logger.LogWarning("Password reset email delivery failed for user {Username}.", user.Username);
                }
            }

            await _audit.LogAsync(
                user.UserId,
                "PASSWORD_RESET_REQUESTED",
                "USER_ACCOUNT",
                user.UserId,
                "Success",
                $"Password reset requested for {user.Username}",
                ipAddress);

            return (true, genericMessage, rawToken, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for {Identifier}", usernameOrEmail);
            return (false, "An error occurred while processing your request.", null, null);
        }
    }

    public async Task<(bool Success, string Error)> ResetPasswordWithTokenAsync(
        string username,
        string token,
        string newPassword,
        string? ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            {
                return (false, "Invalid password reset parameters.");
            }

            string normalized = username.Trim();
            var user = await _context.UserAccounts
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Username == normalized);

            if (user == null)
            {
                return (false, "Account not found.");
            }

            if (string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) ||
                !user.PasswordResetTokenExpiry.HasValue)
            {
                return (false, "No active password reset request found for this account.");
            }

            if (user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
            {
                return (false, "This password reset token has expired. Please submit a new request.");
            }

            if (!SecurityTokenHelper.VerifyToken(token, user.PasswordResetTokenHash))
            {
                return (false, "Invalid or tampered password reset token.");
            }

            // Set new password
            user.PasswordHash = _hasher.HashPassword(user, newPassword);

            // Invalidate/consume token (single-use)
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiry = null;

            // Reset failed login attempts and lockout
            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;

            // If the account was Inactive solely pending initial email verification (not administratively Locked),
            // completing a verified email password reset confirms email ownership and activates the account.
            if (!user.EmailConfirmed &&
                string.Equals(user.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(user.EmailVerificationTokenHash))
            {
                user.EmailConfirmed = true;
                user.AccountStatus = "Active";
                user.EmailVerificationTokenHash = null;
                user.EmailVerificationTokenExpiry = null;
            }

            // Note: Preserve administrative locks if marked Inactive (after confirmation) or Locked
            await _context.SaveChangesAsync();

            // Send password-changed confirmation notification
            if (_emailSender != null && user.Employee != null && !string.IsNullOrWhiteSpace(user.Employee.Email))
            {
                string notifySubject = "SEPS Password Changed Successfully";
                string notifyBody = EmailTemplateService.BuildPasswordChangedNotification(user.Username, DateTime.UtcNow, ipAddress);
                await _emailSender.SendEmailAsync(user.Employee.Email, notifySubject, notifyBody);
            }

            await _audit.LogAsync(
                user.UserId,
                "PASSWORD_RESET_TOKEN_COMPLETED",
                "USER_ACCOUNT",
                user.UserId,
                "Success",
                $"User {user.Username} reset password via verified token.",
                ipAddress);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {Username}", username);
            return (false, "An error occurred while resetting the password.");
        }
    }

    public async Task<(bool Success, string Error, int UserId)> CreateUserAccountAsync(
        SecureEmployeePayrollSystem.ViewModels.CreateUserViewModel model,
        int currentUserId,
        string? ipAddress)
    {
        try
        {
            // 1. Check username uniqueness
            bool usernameExists = await _context.UserAccounts
                .AnyAsync(u => u.Username.ToLower() == model.Username.Trim().ToLower());

            if (usernameExists)
            {
                return (false, "Username is already taken by another account.", 0);
            }

            // 2. Validate Role exists
            var role = await _context.AppRoles.FindAsync(model.RoleId);
            if (role == null)
            {
                return (false, "Selected role does not exist.", 0);
            }

            // 3. Resolve or Create Linked Employee (UserAccount requires valid Employee_ID)
            int targetEmployeeId = 0;
            if (model.EmployeeId.HasValue && model.EmployeeId.Value > 0)
            {
                bool employeeExists = await _context.Employees.AnyAsync(e => e.EmployeeId == model.EmployeeId.Value);
                if (!employeeExists)
                {
                    return (false, "Selected employee does not exist.", 0);
                }

                bool alreadyLinked = await _context.UserAccounts.AnyAsync(u => u.EmployeeId == model.EmployeeId.Value);
                if (alreadyLinked)
                {
                    return (false, "Selected employee already has a linked user account.", 0);
                }

                targetEmployeeId = model.EmployeeId.Value;
            }
            else
            {
                string[] nameParts = model.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                string fName = nameParts.Length > 0 ? nameParts[0] : model.Username;
                string lName = nameParts.Length > 1 ? nameParts[1] : ".";

                var defaultDept = await _context.Departments.FirstOrDefaultAsync();
                int deptId = model.DepartmentId.HasValue && model.DepartmentId.Value > 0
                    ? model.DepartmentId.Value
                    : (defaultDept?.DepartmentId ?? 1);

                var defaultDesig = await _context.Designations.FirstOrDefaultAsync();
                int desigId = defaultDesig?.DesignationId ?? 1;

                var defaultSalary = await _context.SalaryStructures.FirstOrDefaultAsync();
                int salaryId = defaultSalary?.SalaryStructureId ?? 1;

                var newEmp = new Employee
                {
                    FirstName = fName,
                    LastName = lName,
                    Email = model.Email.Trim(),
                    Phone = string.IsNullOrWhiteSpace(model.PhoneNumber) ? "0000000000" : model.PhoneNumber.Trim(),
                    Address = "Corporate Headquarters",
                    Gender = "Not Specified",
                    DateOfBirth = new DateOnly(1995, 1, 1),
                    HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    EmploymentStatus = "Active",
                    DepartmentId = deptId,
                    DesignationId = desigId,
                    SalaryStructureId = salaryId
                };

                _context.Employees.Add(newEmp);
                await _context.SaveChangesAsync();
                targetEmployeeId = newEmp.EmployeeId;
            }

            // 4. Create UserAccount
            bool isActive = model.AccountStatus != "Inactive";
            var user = new UserAccount
            {
                Username = model.Username.Trim(),
                EmployeeId = targetEmployeeId,
                AccountStatus = isActive ? "Active" : "Inactive",
                EmailConfirmed = isActive,
                FailedLoginAttempts = 0,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _hasher.HashPassword(user, model.Password);

            _context.UserAccounts.Add(user);
            await _context.SaveChangesAsync();

            // 5. Assign Selected Role
            var userRole = new UserRole
            {
                UserId = user.UserId,
                RoleId = model.RoleId,
                AssignedAt = DateTime.UtcNow
            };
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            // 6. Audit Trail
            await _audit.LogAsync(
                currentUserId,
                "CREATE",
                "USER_ACCOUNT",
                user.UserId,
                "Success",
                $"Admin created user '{user.Username}' with role '{role.RoleName}' (Status: {user.AccountStatus})",
                ipAddress);

            return (true, string.Empty, user.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user account {Username}", model.Username);
            return (false, "Database error: " + ex.Message, 0);
        }
    }
}
