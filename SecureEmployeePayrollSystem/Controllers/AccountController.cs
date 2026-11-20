using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;
using SecureEmployeePayrollSystem.ViewModels;

namespace SecureEmployeePayrollSystem.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly AccountAuthService _authService;
    private readonly UserManagementService _userService;
    private readonly MfaService _mfaService;
    private readonly PayrollDbContext _dbContext;
    private readonly AuditService _audit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AccountAuthService authService,
        UserManagementService userService,
        MfaService mfaService,
        PayrollDbContext dbContext,
        AuditService audit,
        ILogger<AccountController> logger)
    {
        _authService = authService;
        _userService = userService;
        _mfaService = mfaService;
        _dbContext = dbContext;
        _audit = audit;
        _logger = logger;
    }

    // ============================================================
    // GET: /Account/Login
    // ============================================================

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl
        };

        return View(model);
    }

    // ============================================================
    // POST: /Account/Login
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginLimiter")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.AuthenticateWithMfaAsync(
            model.UsernameOrEmail,
            model.Password,
            model.RememberMe);

        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (!result.Success || result.User == null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        // Server-enforced MFA challenge handling
        if (result.RequiresMfa)
        {
            var challenge = _mfaService.CreateChallenge(
                result.User,
                model.RememberMe,
                model.ReturnUrl,
                isEnrolling: result.RequiresEnrollment);

            if (result.RequiresEnrollment)
            {
                TempData["InfoMessage"] = "Your role policy mandates Multi-Factor Authentication (MFA). Please complete enrollment before accessing your account.";
                return RedirectToAction(nameof(EnrollMfa), new { challengeToken = challenge.ChallengeToken });
            }

            // If user's MFA method is EMAIL, dispatch OTP automatically
            if (string.Equals(result.User.MfaType, "EMAIL", StringComparison.OrdinalIgnoreCase))
            {
                await _mfaService.SendEmailOtpAsync(result.User, clientIp);
            }

            return RedirectToAction(nameof(MfaChallenge), new { challengeToken = challenge.ChallengeToken });
        }

        // Direct login without MFA (non-privileged role without MFA enabled)
        var authenticationProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            result.Principal!,
            authenticationProperties);

        _logger.LogInformation("User {Username} signed in successfully.", model.UsernameOrEmail);

        await _audit.LogAsync(
            result.User.UserId,
            "LOGIN",
            "USER_ACCOUNT",
            result.User.UserId,
            "Success",
            $"User {model.UsernameOrEmail} signed in successfully.",
            clientIp);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // ============================================================
    // GET: /Account/MfaChallenge
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> MfaChallenge(string? challengeToken)
    {
        var challenge = _mfaService.GetChallenge(challengeToken);
        if (challenge == null)
        {
            TempData["ErrorMessage"] = "Authentication session expired or invalid. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _dbContext.UserAccounts
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.UserId == challenge.UserId);

        if (user == null)
        {
            return RedirectToAction(nameof(Login));
        }

        bool hasTotp = !string.IsNullOrWhiteSpace(user.MfaSecretKey);
        bool hasEmail = user.Employee != null && !string.IsNullOrWhiteSpace(user.Employee.Email);
        bool hasRecovery = !string.IsNullOrWhiteSpace(user.MfaRecoveryCodes);

        string defaultMethod = "TOTP";
        if (string.Equals(user.MfaType, "EMAIL", StringComparison.OrdinalIgnoreCase) && hasEmail)
        {
            defaultMethod = "EMAIL";
        }
        else if (!hasTotp && hasEmail)
        {
            defaultMethod = "EMAIL";
        }

        var model = new MfaChallengeViewModel
        {
            ChallengeToken = challenge.ChallengeToken,
            Method = defaultMethod,
            HasTotp = hasTotp,
            HasEmail = hasEmail,
            HasRecovery = hasRecovery,
            MaskedEmail = hasEmail ? _mfaService.MaskEmail(user.Employee!.Email) : null,
            RememberMe = challenge.RememberMe,
            ReturnUrl = challenge.ReturnUrl
        };

        return View(model);
    }

    // ============================================================
    // POST: /Account/MfaChallenge
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("MfaLimiter")]
    public async Task<IActionResult> MfaChallenge(MfaChallengeViewModel model)
    {
        var challenge = _mfaService.GetChallenge(model.ChallengeToken);
        if (challenge == null)
        {
            TempData["ErrorMessage"] = "Authentication session expired. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _dbContext.UserAccounts
            .Include(u => u.UserRoleUsers).ThenInclude(ur => ur.Role)
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.UserId == challenge.UserId);

        if (user == null)
        {
            return RedirectToAction(nameof(Login));
        }

        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        bool verificationSuccess = false;
        string errorMessage = "Invalid verification code.";

        if (string.Equals(model.Method, "TOTP", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(user.MfaSecretKey))
            {
                errorMessage = "Authenticator App is not configured for this account.";
            }
            else
            {
                verificationSuccess = _mfaService.VerifyTotp(user.MfaSecretKey, model.Code, user.UserId);
                if (!verificationSuccess)
                {
                    errorMessage = "Invalid authenticator code. Check that your device time is accurate and try again.";
                }
            }
        }
        else if (string.Equals(model.Method, "EMAIL", StringComparison.OrdinalIgnoreCase))
        {
            var (emailOk, emailErr) = await _mfaService.VerifyEmailOtpAsync(user, model.Code, clientIp);
            verificationSuccess = emailOk;
            if (!verificationSuccess)
            {
                errorMessage = emailErr;
            }
        }
        else if (string.Equals(model.Method, "RECOVERY", StringComparison.OrdinalIgnoreCase))
        {
            verificationSuccess = await _mfaService.VerifyAndConsumeRecoveryCodeAsync(user, model.Code, clientIp);
            if (!verificationSuccess)
            {
                errorMessage = "Invalid emergency recovery code or code has already been consumed.";
            }
        }

        if (!verificationSuccess)
        {
            challenge.FailedAttempts++;
            if (challenge.FailedAttempts >= 5)
            {
                _mfaService.InvalidateChallenge(model.ChallengeToken);
                await _audit.LogAsync(
                    user.UserId,
                    "MFA_LOCKED",
                    "USER_ACCOUNT",
                    user.UserId,
                    "Failure",
                    $"User exceeded 5 consecutive MFA attempts from IP {clientIp}.",
                    clientIp);

                TempData["ErrorMessage"] = "Too many failed MFA verification attempts. For your security, this session has been terminated.";
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(string.Empty, $"{errorMessage} ({5 - challenge.FailedAttempts} attempt(s) remaining)");

            model.HasTotp = !string.IsNullOrWhiteSpace(user.MfaSecretKey);
            model.HasEmail = user.Employee != null && !string.IsNullOrWhiteSpace(user.Employee.Email);
            model.HasRecovery = !string.IsNullOrWhiteSpace(user.MfaRecoveryCodes);
            model.MaskedEmail = model.HasEmail ? _mfaService.MaskEmail(user.Employee!.Email) : null;
            return View(model);
        }

        // Verification Succeeded!
        _mfaService.InvalidateChallenge(model.ChallengeToken);

        var principal = _authService.CreateClaimsPrincipal(user);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = challenge.RememberMe,
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        _logger.LogInformation("User {Username} completed MFA verification using method {Method}.", user.Username, model.Method);

        await _audit.LogAsync(
            user.UserId,
            "LOGIN_MFA_SUCCESS",
            "USER_ACCOUNT",
            user.UserId,
            "Success",
            $"User {user.Username} signed in with {model.Method} verification.",
            clientIp);

        if (!string.IsNullOrWhiteSpace(challenge.ReturnUrl) && Url.IsLocalUrl(challenge.ReturnUrl))
        {
            return Redirect(challenge.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // ============================================================
    // POST: /Account/ResendEmailOtp
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("OtpResendLimiter")]
    public async Task<IActionResult> ResendEmailOtp(string challengeToken)
    {
        var challenge = _mfaService.GetChallenge(challengeToken);
        if (challenge == null)
        {
            return Json(new { success = false, message = "Session expired. Please sign in again." });
        }

        var user = await _dbContext.UserAccounts
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.UserId == challenge.UserId);

        if (user == null || user.Employee == null || string.IsNullOrWhiteSpace(user.Employee.Email))
        {
            return Json(new { success = false, message = "No valid email address linked to this account." });
        }

        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (sent, msg) = await _mfaService.SendEmailOtpAsync(user, clientIp);
        return Json(new { success = sent, message = msg });
    }

    // ============================================================
    // GET: /Account/EnrollMfa
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> EnrollMfa(string? challengeToken)
    {
        MfaChallenge? challenge = null;
        UserAccount? user = null;

        if (!string.IsNullOrWhiteSpace(challengeToken))
        {
            challenge = _mfaService.GetChallenge(challengeToken);
            if (challenge != null)
            {
                user = await _dbContext.UserAccounts
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == challenge.UserId);
            }
        }
        else if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int currentUserId))
            {
                user = await _dbContext.UserAccounts
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == currentUserId);
            }
        }

        if (user == null)
        {
            TempData["ErrorMessage"] = "You must have an active authentication session to enroll in MFA.";
            return RedirectToAction(nameof(Login));
        }

        string secretKey = _mfaService.GenerateSecretKey();
        string qrUri = _mfaService.GenerateQrCodeDataUri(user.Username, secretKey);
        var recoveryCodes = _mfaService.GenerateRecoveryCodes();

        if (challenge != null)
        {
            challenge.PendingSecretKey = secretKey;
            challenge.PendingRecoveryCodes = recoveryCodes;
        }
        else
        {
            HttpContext.Session.SetString("SEPS_PENDING_MFA_SECRET", secretKey);
            HttpContext.Session.SetString("SEPS_PENDING_MFA_RECOVERY", string.Join(";", recoveryCodes));
        }

        var model = new MfaEnrollViewModel
        {
            ChallengeToken = challengeToken,
            SecretKey = secretKey,
            ManualEntryKey = secretKey,
            QrCodeDataUri = qrUri,
            RecoveryCodes = recoveryCodes,
            EmailAddress = user.Employee?.Email,
            MaskedEmail = user.Employee?.Email != null ? _mfaService.MaskEmail(user.Employee.Email) : null,
            SelectedMethod = "TOTP"
        };

        return View(model);
    }

    // ============================================================
    // POST: /Account/EnrollMfa
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollMfa(MfaEnrollViewModel model)
    {
        MfaChallenge? challenge = null;
        UserAccount? user = null;

        if (!string.IsNullOrWhiteSpace(model.ChallengeToken))
        {
            challenge = _mfaService.GetChallenge(model.ChallengeToken);
            if (challenge != null)
            {
                user = await _dbContext.UserAccounts
                    .Include(u => u.UserRoleUsers).ThenInclude(ur => ur.Role)
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == challenge.UserId);
            }
        }
        else if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int currentUserId))
            {
                user = await _dbContext.UserAccounts
                    .Include(u => u.UserRoleUsers).ThenInclude(ur => ur.Role)
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == currentUserId);
            }
        }

        if (user == null)
        {
            TempData["ErrorMessage"] = "Enrollment session expired. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        string secretKey = challenge?.PendingSecretKey ?? HttpContext.Session.GetString("SEPS_PENDING_MFA_SECRET") ?? model.SecretKey;
        List<string> recoveryCodes = challenge?.PendingRecoveryCodes 
            ?? (HttpContext.Session.GetString("SEPS_PENDING_MFA_RECOVERY")?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
            ?? model.RecoveryCodes;

        if (string.IsNullOrWhiteSpace(secretKey) || recoveryCodes == null || recoveryCodes.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Enrollment session expired. Please restart MFA setup.");
            return View(model);
        }

        var (success, error) = await _mfaService.CompleteTotpEnrollmentAsync(
            user,
            secretKey,
            model.VerificationCode,
            recoveryCodes,
            clientIp);

        if (!success)
        {
            ModelState.AddModelError(nameof(model.VerificationCode), error);
            model.SecretKey = secretKey;
            model.ManualEntryKey = secretKey;
            model.QrCodeDataUri = _mfaService.GenerateQrCodeDataUri(user.Username, secretKey);
            model.RecoveryCodes = recoveryCodes;
            return View(model);
        }

        if (challenge != null)
        {
            _mfaService.InvalidateChallenge(challenge.ChallengeToken);

            var principal = _authService.CreateClaimsPrincipal(user);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = challenge.RememberMe,
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            TempData["SuccessMessage"] = "MFA setup complete! Multi-Factor Authentication is now active on your account.";
            return RedirectToAction("Index", "Home");
        }

        TempData["SuccessMessage"] = "Authenticator app configured successfully!";
        return RedirectToAction("Index", "Settings");
    }

    // ============================================================
    // POST: /Account/Logout
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var username = User.Identity?.Name;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (int.TryParse(userIdClaim, out int userId))
        {
            await _audit.LogAsync(
                userId,
                "LOGOUT",
                "USER_ACCOUNT",
                userId,
                "Success",
                $"User {username} logged out.",
                clientIp);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation("User {Username} logged out.", username);

        return RedirectToAction(nameof(Login));
    }

    // ============================================================
    // GET: /Account/Register
    // ============================================================

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel());
    }

    // ============================================================
    // POST: /Account/Register
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _userService.RegisterEmployeeAccountAsync(
            model.EmployeeId,
            model.Email,
            model.Username,
            model.Password,
            clientIp);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        TempData["SuccessMessage"] = "Account registered successfully! A verification link has been sent to your registered work email. Please click the link to activate your account before signing in.";
        return RedirectToAction(nameof(Login));
    }

    // ============================================================
    // GET: /Account/VerifyEmail
    // ============================================================

    [HttpGet]
    [EnableRateLimiting("EmailVerificationLimiter")]
    public async Task<IActionResult> VerifyEmail(int userId, string? token)
    {
        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (userId <= 0 || string.IsNullOrWhiteSpace(token))
        {
            var invalidModel = new VerifyEmailViewModel
            {
                IsSuccess = false,
                Message = "Invalid or incomplete verification link."
            };
            return View(invalidModel);
        }

        var result = await _userService.VerifyEmailAsync(userId, token, clientIp);

        var model = new VerifyEmailViewModel
        {
            IsSuccess = result.Success,
            Message = result.Message,
            Username = result.Username
        };

        return View(model);
    }

    // ============================================================
    // GET: /Account/ForgotPassword
    // ============================================================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    // ============================================================
    // POST: /Account/ForgotPassword
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("PasswordResetLimiter")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _userService.GeneratePasswordResetTokenAsync(model.UsernameOrEmail, clientIp);

        ViewBag.Message = result.Message;
        return View(model);
    }

    // ============================================================
    // GET: /Account/ResetPassword
    // ============================================================

    [HttpGet]
    [EnableRateLimiting("PasswordResetLimiter")]
    public async Task<IActionResult> ResetPassword(string? token, string? username)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(username))
        {
            TempData["ErrorMessage"] = "Password reset link is invalid or incomplete. Please request a new link.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.Username == username.Trim());

        if (user == null ||
            string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) ||
            !user.PasswordResetTokenExpiry.HasValue ||
            user.PasswordResetTokenExpiry.Value < DateTime.UtcNow ||
            !SecurityTokenHelper.VerifyToken(token, user.PasswordResetTokenHash))
        {
            TempData["ErrorMessage"] = "Password reset link is invalid, expired, or has already been used. Please request a new link.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var model = new ResetPasswordViewModel
        {
            Username = username.Trim(),
            Token = token
        };

        return View(model);
    }

    // ============================================================
    // POST: /Account/ResetPassword
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("PasswordResetLimiter")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _userService.ResetPasswordWithTokenAsync(
            model.Username,
            model.Token,
            model.NewPassword,
            clientIp);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        TempData["SuccessMessage"] = "Your password has been reset successfully! You can now sign in with your new credentials.";
        return RedirectToAction(nameof(Login));
    }

    // ============================================================
    // GET: /Account/AccessDenied
    // ============================================================

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}