using System.Buffers.Binary;
using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class AccountAuthService
{
    private readonly PayrollDbContext _context;
    private readonly ILogger<AccountAuthService> _logger;
    private readonly IConfiguration _config;
    private readonly MfaService? _mfaService;
    private readonly PasswordHasher<UserAccount> _hasher;

    public AccountAuthService(
        PayrollDbContext context,
        ILogger<AccountAuthService> logger,
        IConfiguration config,
        MfaService? mfaService = null)
    {
        _context = context;
        _logger = logger;
        _config = config;
        _mfaService = mfaService;

        _hasher = new PasswordHasher<UserAccount>();
    }

    public class AuthWithMfaResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public ClaimsPrincipal? Principal { get; set; }
        public bool RequiresMfa { get; set; }
        public bool RequiresEnrollment { get; set; }
        public UserAccount? User { get; set; }
    }

    // ============================================================
    // LOGIN
    // ============================================================

    public async Task<AuthWithMfaResult> AuthenticateWithMfaAsync(
        string usernameOrEmail,
        string password,
        bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail) ||
            string.IsNullOrEmpty(password))
        {
            return new AuthWithMfaResult
            {
                Success = false,
                Error = "Invalid username or password."
            };
        }

        string normalizedIdentifier = usernameOrEmail.Trim();

        var user =
            await _context.UserAccounts
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u =>
                    u.Username == normalizedIdentifier ||
                    (
                        u.Employee != null &&
                        u.Employee.Email == normalizedIdentifier
                    ));

        if (user == null)
        {
            _logger.LogWarning(
                "Login failed. User not found: {Username}",
                usernameOrEmail);

            return new AuthWithMfaResult
            {
                Success = false,
                Error = "Invalid username or password."
            };
        }

        if (!string.Equals(
                user.AccountStatus,
                "Active",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!user.EmailConfirmed)
            {
                return new AuthWithMfaResult
                {
                    Success = false,
                    Error = "Your account is pending verification. Please verify your email address using the activation link sent to your inbox."
                };
            }

            return new AuthWithMfaResult
            {
                Success = false,
                Error = "Account is locked or inactive. Please contact your system administrator."
            };
        }

        if (!user.EmailConfirmed)
        {
            return new AuthWithMfaResult
            {
                Success = false,
                Error = "Your account is pending verification. Please verify your email address using the activation link sent to your inbox."
            };
        }

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            _logger.LogWarning(
                "Login blocked for user {Username}: account temporarily locked until {LockoutEndUtc}.",
                user.Username,
                user.LockoutEndUtc.Value);

            return new AuthWithMfaResult
            {
                Success = false,
                Error = "Too many failed login attempts. Please wait 30 seconds before trying again."
            };
        }

        PasswordVerificationResult verificationResult;

        try
        {
            verificationResult =
                _hasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    password);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Password verification error for {Username}.",
                user.Username);

            verificationResult =
                PasswordVerificationResult.Failed;
        }

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            try
            {
                bool legacyPasswordValid =
                    TryVerifyIdentityV3Pbkdf2Sha512(
                        user.PasswordHash,
                        password);

                if (!legacyPasswordValid)
                {
                    string errorMsg = await RecordFailedLoginAsync(user);

                    return new AuthWithMfaResult
                    {
                        Success = false,
                        Error = errorMsg
                    };
                }

                user.PasswordHash =
                    _hasher.HashPassword(
                        user,
                        password);

                _logger.LogInformation(
                    "Password hash migrated for {Username}.",
                    user.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Legacy password verification failed for {Username}.",
                    user.Username);

                string errorMsg = await RecordFailedLoginAsync(user);

                return new AuthWithMfaResult
                {
                    Success = false,
                    Error = errorMsg
                };
            }
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash =
                _hasher.HashPassword(
                    user,
                    password);
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.LastLogin = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Server-side MFA policy enforcement
        if (_mfaService != null && _mfaService.IsMfaRequired(user))
        {
            bool isEnrolled = _mfaService.IsMfaEnrolled(user);
            _logger.LogInformation(
                "User {Username} requires MFA. Enrolled: {Enrolled}",
                user.Username,
                isEnrolled);

            return new AuthWithMfaResult
            {
                Success = true,
                RequiresMfa = true,
                RequiresEnrollment = !isEnrolled,
                User = user,
                Principal = null // Zero cookie issuance until MFA challenge passes
            };
        }

        var principal = CreateClaimsPrincipal(user);

        _logger.LogInformation(
            "User {Username} authenticated directly without MFA.",
            user.Username);

        return new AuthWithMfaResult
        {
            Success = true,
            RequiresMfa = false,
            RequiresEnrollment = false,
            User = user,
            Principal = principal
        };
    }

    public async Task<(
        bool Success,
        string Error,
        ClaimsPrincipal? Principal
    )> AuthenticateAsync(
        string usernameOrEmail,
        string password,
        bool rememberMe)
    {
        var res = await AuthenticateWithMfaAsync(usernameOrEmail, password, rememberMe);
        if (!res.Success)
        {
            return (false, res.Error, null);
        }

        var principal = res.Principal ?? (res.User != null ? CreateClaimsPrincipal(res.User) : null);
        return (true, string.Empty, principal);
    }

    public ClaimsPrincipal CreateClaimsPrincipal(UserAccount user)
    {
        var claims =
            new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserId.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    user.Username)
            };

        if (user.Employee != null &&
            !string.IsNullOrWhiteSpace(
                user.Employee.Email))
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Email,
                    user.Employee.Email));
        }

        claims.Add(
            new Claim(
                "EmployeeId",
                user.EmployeeId.ToString()));

        var roles =
            user.UserRoleUsers?
                .Select(ur => ur.Role?.RoleName)
                .Where(role =>
                    !string.IsNullOrWhiteSpace(role))
                .Distinct();

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.Role,
                        role!));
            }
        }

        var identity =
            new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults
                    .AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }

    // ============================================================
    // RECORD FAILED LOGIN
    // ============================================================

    private async Task<string> RecordFailedLoginAsync(
        UserAccount user)
    {
        user.FailedLoginAttempts++;

        int threshold = 5;

        var configuredThreshold =
            _config[
                "Authentication:FailedLoginThreshold"];

        if (int.TryParse(
                configuredThreshold,
                out int configuredValue))
        {
            threshold = configuredValue;
        }

        int lockoutDurationSeconds = 30;
        var configuredDuration =
            _config[
                "Authentication:LockoutDurationSeconds"];

        if (int.TryParse(
                configuredDuration,
                out int configuredDurationVal))
        {
            lockoutDurationSeconds = configuredDurationVal;
        }

        string errorMessage;

        if (user.FailedLoginAttempts >= threshold)
        {
            user.LockoutEndUtc = DateTime.UtcNow.AddSeconds(lockoutDurationSeconds);
            errorMessage = "Too many failed login attempts. Please wait 30 seconds before trying again.";

            _logger.LogWarning(
                "User {Username} temporarily locked until {LockoutEndUtc} after {Attempts} consecutive failed attempts.",
                user.Username,
                user.LockoutEndUtc,
                user.FailedLoginAttempts);
        }
        else
        {
            errorMessage = "Invalid username or password.";
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Concurrency conflict while recording failed login for {Username}.",
                user.Username);
        }

        return errorMessage;
    }

    // ============================================================
    // LEGACY IDENTITY V3 PBKDF2-SHA512 VERIFICATION
    // ============================================================

    private bool TryVerifyIdentityV3Pbkdf2Sha512(
        string storedHash,
        string password)
    {
        if (string.IsNullOrWhiteSpace(storedHash) ||
            string.IsNullOrEmpty(password))
        {
            return false;
        }

        byte[] decoded;

        // ========================================================
        // BASE64 DECODE
        // ========================================================

        try
        {
            decoded =
                Convert.FromBase64String(
                    storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        // ========================================================
        // MINIMUM HASH SIZE
        // ========================================================

        if (decoded.Length < 13)
        {
            return false;
        }

        // ========================================================
        // IDENTITY V3 FORMAT MARKER
        // ========================================================

        if (decoded[0] != 0x01)
        {
            return false;
        }

        try
        {
            // ====================================================
            // READ IDENTITY V3 HEADER
            // ====================================================

            uint prf =
                BinaryPrimitives
                    .ReadUInt32BigEndian(
                        decoded.AsSpan(1, 4));

            uint iterations =
                BinaryPrimitives
                    .ReadUInt32BigEndian(
                        decoded.AsSpan(5, 4));

            uint saltLength =
                BinaryPrimitives
                    .ReadUInt32BigEndian(
                        decoded.AsSpan(9, 4));

            // ====================================================
            // PRF 3 = HMAC-SHA512
            // ====================================================

            if (prf != 3)
            {
                return false;
            }

            if (iterations == 0)
            {
                return false;
            }

            if (saltLength == 0)
            {
                return false;
            }

            if (saltLength >
                decoded.Length - 13)
            {
                return false;
            }

            // ====================================================
            // SALT
            // ====================================================

            int saltOffset = 13;

            int saltSize =
                checked((int)saltLength);

            // ====================================================
            // SUBKEY
            // ====================================================

            int subkeyOffset =
                saltOffset + saltSize;

            int subkeyLength =
                decoded.Length - subkeyOffset;

            if (subkeyLength <= 0)
            {
                return false;
            }

            // ====================================================
            // COPY SALT
            // ====================================================

            var salt =
                new byte[saltSize];

            Buffer.BlockCopy(
                decoded,
                saltOffset,
                salt,
                0,
                saltSize);

            // ====================================================
            // COPY EXPECTED SUBKEY
            // ====================================================

            var expectedSubkey =
                new byte[subkeyLength];

            Buffer.BlockCopy(
                decoded,
                subkeyOffset,
                expectedSubkey,
                0,
                subkeyLength);

            // ====================================================
            // PBKDF2-SHA512
            // ====================================================

            using var derive =
                new Rfc2898DeriveBytes(
                    password,
                    salt,
                    checked((int)iterations),
                    HashAlgorithmName.SHA512);

            var generatedSubkey =
                derive.GetBytes(
                    subkeyLength);

            // ====================================================
            // CONSTANT-TIME COMPARISON
            // ====================================================

            return CryptographicOperations
                .FixedTimeEquals(
                    generatedSubkey,
                    expectedSubkey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Legacy password hash verification failed.");

            return false;
        }
    }
}