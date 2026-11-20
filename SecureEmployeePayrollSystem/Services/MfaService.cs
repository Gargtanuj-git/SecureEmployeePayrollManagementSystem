using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class MfaChallenge
{
    public string ChallengeToken { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);
    public int FailedAttempts { get; set; }
    public long LastVerifiedTimeStep { get; set; }
    public bool IsEnrolling { get; set; }
    public string? PendingSecretKey { get; set; }
    public List<string>? PendingRecoveryCodes { get; set; }
}

public class MfaService
{
    private readonly PayrollDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly AuditService _audit;
    private readonly ILogger<MfaService> _logger;
    private readonly IDataProtector? _protector;

    // Server-managed HMAC-SHA256 Pepper Key for OTP integrity
    private static readonly byte[] _otpPepperKey = SHA256.HashData(Encoding.UTF8.GetBytes("SEPS_PEPPER_KEY_MFA_2026_ENTERPRISE"));

    // Thread-safe in-memory challenge store with expiration
    private static readonly ConcurrentDictionary<string, MfaChallenge> _challenges = new();

    // Replay protection tracking: UserId -> Last verified TOTP time step
    private static readonly ConcurrentDictionary<int, long> _userLastTotpTimeSteps = new();

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public MfaService(
        PayrollDbContext context,
        IEmailSender emailSender,
        AuditService audit,
        ILogger<MfaService> logger,
        IDataProtectionProvider? dataProtectionProvider = null)
    {
        _context = context;
        _emailSender = emailSender;
        _audit = audit;
        _logger = logger;

        if (dataProtectionProvider != null)
        {
            _protector = dataProtectionProvider.CreateProtector("SEPS.MfaService.SecretKeyProtector.v1");
        }
    }

    public string EncryptSecret(string secret)
    {
        if (_protector == null || string.IsNullOrWhiteSpace(secret)) return secret;
        return "ENC:" + _protector.Protect(secret);
    }

    public string DecryptSecret(string encryptedOrPlainSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedOrPlainSecret)) return string.Empty;
        if (encryptedOrPlainSecret.StartsWith("ENC:") && _protector != null)
        {
            try
            {
                return _protector.Unprotect(encryptedOrPlainSecret[4..]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt MFA secret key with Data Protection.");
                return string.Empty;
            }
        }
        return encryptedOrPlainSecret;
    }

    // ============================================================
    // MFA POLICY ENFORCEMENT
    // ============================================================

    public bool IsMfaRequired(UserAccount user)
    {
        return user.MfaEnabled;
    }

    public bool IsMfaEnrolled(UserAccount user)
    {
        if (!string.IsNullOrWhiteSpace(user.MfaSecretKey))
            return true;

        if (user.EmailConfirmed && string.Equals(user.MfaType, "EMAIL", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    // ============================================================
    // CHALLENGE MANAGEMENT
    // ============================================================

    public MfaChallenge CreateChallenge(UserAccount user, bool rememberMe, string? returnUrl, bool isEnrolling = false)
    {
        CleanExpiredChallenges();

        byte[] randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        string token = Convert.ToBase64String(randomBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var challenge = new MfaChallenge
        {
            ChallengeToken = token,
            UserId = user.UserId,
            Username = user.Username,
            RememberMe = rememberMe,
            ReturnUrl = returnUrl,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            FailedAttempts = 0,
            IsEnrolling = isEnrolling
        };

        _challenges[token] = challenge;
        return challenge;
    }

    public MfaChallenge? GetChallenge(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        if (_challenges.TryGetValue(token, out var challenge))
        {
            if (challenge.ExpiresAt < DateTime.UtcNow)
            {
                _challenges.TryRemove(token, out _);
                return null;
            }
            return challenge;
        }

        return null;
    }

    public void InvalidateChallenge(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _challenges.TryRemove(token, out _);
        }
    }

    private void CleanExpiredChallenges()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _challenges)
        {
            if (kvp.Value.ExpiresAt < now)
            {
                _challenges.TryRemove(kvp.Key, out _);
            }
        }
    }

    // ============================================================
    // RFC 6238 TOTP ALGORITHM & QR CODE GENERATION
    // ============================================================

    public string GenerateSecretKey()
    {
        byte[] bytes = new byte[20]; // 160-bit secret
        RandomNumberGenerator.Fill(bytes);
        return ToBase32String(bytes);
    }

    public string GenerateQrCodeDataUri(string username, string secretKey)
    {
        string encodedUser = Uri.EscapeDataString(username);
        string uri = $"otpauth://totp/SEPS:{encodedUser}?secret={secretKey}&issuer=SEPS&algorithm=SHA1&digits=6&period=30";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeBytes = qrCode.GetGraphic(20);

        return "data:image/png;base64," + Convert.ToBase64String(qrCodeBytes);
    }

    public bool VerifyTotp(string secretKey, string code, int userId)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(code))
            return false;

        secretKey = DecryptSecret(secretKey);
        code = code.Trim().Replace(" ", "").Replace("-", "");
        if (code.Length != 6 || !int.TryParse(code, out _))
            return false;

        byte[] keyBytes;
        try
        {
            keyBytes = FromBase32String(secretKey);
        }
        catch
        {
            return false;
        }

        long currentTimeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        // Tolerance window: current step - 1, current step, current step + 1
        for (long step = currentTimeStep - 1; step <= currentTimeStep + 1; step++)
        {
            string expectedCode = GenerateTotpForStep(keyBytes, step);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedCode),
                    Encoding.UTF8.GetBytes(code)))
            {
                // Replay protection: ensure this time step wasn't already used by this user
                if (_userLastTotpTimeSteps.TryGetValue(userId, out long lastUsedStep) && lastUsedStep >= step)
                {
                    _logger.LogWarning("TOTP replay attempt detected for User ID {UserId} on time step {Step}", userId, step);
                    return false;
                }

                _userLastTotpTimeSteps[userId] = step;
                return true;
            }
        }

        return false;
    }

    private string GenerateTotpForStep(byte[] key, long timeStep)
    {
        byte[] counter = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter); // Big-endian per RFC 4226 / 6238
        }

        using var hmac = new HMACSHA1(key);
        byte[] hash = hmac.ComputeHash(counter);

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    // ============================================================
    // EMAIL OTP PROVISIONING & VERIFICATION
    // ============================================================

    public async Task<(bool Success, string Message)> SendEmailOtpAsync(UserAccount user, string? ipAddress)
    {
        if (user.Employee == null || string.IsNullOrWhiteSpace(user.Employee.Email))
        {
            return (false, "No registered work email is linked to this account.");
        }

        // Rate limit: If an unexpired OTP was generated less than 60 seconds ago, reject spam
        if (user.EmailOtpExpiry.HasValue && user.EmailOtpExpiry.Value > DateTime.UtcNow.AddMinutes(4))
        {
            return (false, "A verification code was recently generated. Please wait 60 seconds before requesting a new code.");
        }

        // Generate cryptographically secure 6-digit code
        int otpInt = RandomNumberGenerator.GetInt32(100000, 1000000);
        string otpCode = otpInt.ToString("D6");

        // Hash OTP before database storage (never store plain OTPs)
        string hashedOtp = HashCode(otpCode);

        user.EmailOtpCode = hashedOtp;
        user.EmailOtpExpiry = DateTime.UtcNow.AddMinutes(5);
        user.EmailOtpAttempts = 0;

        await _context.SaveChangesAsync();

        string subject = "SEPS Security Verification Code";
        string htmlBody = EmailTemplateService.BuildMfaOtpEmail(user.Username, otpCode, 5, ipAddress);

        bool emailDelivered = await _emailSender.SendEmailAsync(user.Employee.Email, subject, htmlBody);
        if (!emailDelivered)
        {
            _logger.LogError("Failed to deliver MFA OTP email to {ToEmail}", user.Employee.Email);
            await _audit.LogAsync(
                user.UserId,
                "MFA_OTP_FAILED",
                "USER_ACCOUNT",
                user.UserId,
                "Failure",
                $"MFA email dispatch failed for {MaskEmail(user.Employee.Email)}",
                ipAddress);

            return (false, "We were unable to deliver your verification email. Please verify your system SMTP settings or contact your administrator.");
        }

        await _audit.LogAsync(
            user.UserId,
            "MFA_OTP_SENT",
            "USER_ACCOUNT",
            user.UserId,
            "Success",
            $"Email OTP sent to {MaskEmail(user.Employee.Email)}",
            ipAddress);

        return (true, $"A 6-digit verification code was sent to {MaskEmail(user.Employee.Email)}.");
    }

    public async Task<(bool Success, string Error)> VerifyEmailOtpAsync(UserAccount user, string code, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(user.EmailOtpCode))
        {
            return (false, "Verification code is missing or no active request exists.");
        }

        if (!user.EmailOtpExpiry.HasValue || user.EmailOtpExpiry.Value < DateTime.UtcNow)
        {
            // Expired code: clean up
            user.EmailOtpCode = null;
            user.EmailOtpExpiry = null;
            await _context.SaveChangesAsync();
            return (false, "The verification code has expired. Please request a new code.");
        }

        user.EmailOtpAttempts++;
        if (user.EmailOtpAttempts > 5)
        {
            user.EmailOtpCode = null;
            user.EmailOtpExpiry = null;
            await _context.SaveChangesAsync();
            return (false, "Too many failed attempts. The verification code has been invalidated. Please request a new code.");
        }

        string hashedInput = HashCode(code.Trim());
        bool isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hashedInput),
            Encoding.UTF8.GetBytes(user.EmailOtpCode));

        if (!isValid)
        {
            await _context.SaveChangesAsync();
            return (false, $"Invalid verification code. {5 - user.EmailOtpAttempts} attempt(s) remaining.");
        }

        // Single-use: burn the code immediately
        user.EmailOtpCode = null;
        user.EmailOtpExpiry = null;
        user.EmailOtpAttempts = 0;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            user.UserId,
            "MFA_OTP_VERIFIED",
            "USER_ACCOUNT",
            user.UserId,
            "Success",
            "Email OTP verification succeeded.",
            ipAddress);

        return (true, string.Empty);
    }

    // ============================================================
    // RECOVERY CODES
    // ============================================================

    public List<string> GenerateRecoveryCodes()
    {
        var codes = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            byte[] bytes = new byte[5];
            RandomNumberGenerator.Fill(bytes);
            string hex = Convert.ToHexString(bytes);
            codes.Add($"{hex[..4]}-{hex[4..]}".ToUpperInvariant());
        }
        return codes;
    }

    public string HashRecoveryCodes(IEnumerable<string> codes)
    {
        var hashedList = codes.Select(c => HashCode(c.Trim().Replace("-", "").ToUpperInvariant()));
        return string.Join(";", hashedList);
    }

    public async Task<bool> VerifyAndConsumeRecoveryCodeAsync(UserAccount user, string code, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(user.MfaRecoveryCodes) || string.IsNullOrWhiteSpace(code))
            return false;

        string normalized = code.Trim().Replace("-", "").ToUpperInvariant();
        string hashedInput = HashCode(normalized);

        var storedHashes = user.MfaRecoveryCodes.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        int matchIndex = -1;

        for (int i = 0; i < storedHashes.Count; i++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(storedHashes[i]),
                    Encoding.UTF8.GetBytes(hashedInput)))
            {
                matchIndex = i;
                break;
            }
        }

        if (matchIndex >= 0)
        {
            // Consume code
            storedHashes.RemoveAt(matchIndex);
            user.MfaRecoveryCodes = string.Join(";", storedHashes);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                user.UserId,
                "MFA_RECOVERY_USED",
                "USER_ACCOUNT",
                user.UserId,
                "Success",
                $"User consumed emergency recovery code. {storedHashes.Count} recovery codes remaining.",
                ipAddress);

            return true;
        }

        return false;
    }

    // ============================================================
    // ENROLLMENT & RECOVERY ASSISTANCE
    // ============================================================

    public async Task<(bool Success, string Error)> CompleteTotpEnrollmentAsync(
        UserAccount user,
        string secretKey,
        string verificationCode,
        List<string> recoveryCodes,
        string? ipAddress)
    {
        bool valid = VerifyTotp(secretKey, verificationCode, user.UserId);
        if (!valid)
        {
            return (false, "Invalid verification code. Please check your authenticator clock and re-enter the code.");
        }

        user.MfaEnabled = true;
        user.MfaType = "TOTP";
        user.MfaSecretKey = EncryptSecret(secretKey);
        user.MfaRecoveryCodes = HashRecoveryCodes(recoveryCodes);
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            user.UserId,
            "MFA_ENROLLED",
            "USER_ACCOUNT",
            user.UserId,
            "Success",
            "User enrolled Google Authenticator TOTP as primary MFA method.",
            ipAddress);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string Error)> CompleteEmailMfaEnrollmentAsync(
        UserAccount user,
        string verificationCode,
        List<string> recoveryCodes,
        string? ipAddress)
    {
        var (verified, error) = await VerifyEmailOtpAsync(user, verificationCode, ipAddress);
        if (!verified)
        {
            return (false, error);
        }

        user.MfaEnabled = true;
        user.MfaType = "EMAIL";
        user.EmailConfirmed = true;
        user.MfaRecoveryCodes = HashRecoveryCodes(recoveryCodes);
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            user.UserId,
            "MFA_ENROLLED",
            "USER_ACCOUNT",
            user.UserId,
            "Success",
            "User verified email control and enrolled Email OTP as primary MFA method.",
            ipAddress);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string Error)> AdminResetMfaAsync(int targetUserId, int adminUserId, string? ipAddress)
    {
        var targetUser = await _context.UserAccounts.FindAsync(targetUserId);
        if (targetUser == null) return (false, "User not found.");

        targetUser.MfaEnabled = false;
        targetUser.MfaSecretKey = null;
        targetUser.MfaType = null;
        targetUser.MfaRecoveryCodes = null;
        targetUser.EmailOtpCode = null;
        targetUser.EmailOtpExpiry = null;
        targetUser.EmailOtpAttempts = 0;

        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            adminUserId,
            "MFA_ADMIN_RESET",
            "USER_ACCOUNT",
            targetUserId,
            "Success",
            $"Administrator reset MFA configuration for user '{targetUser.Username}'",
            ipAddress);

        return (true, string.Empty);
    }

    // ============================================================
    // UTILITY METHODS
    // ============================================================

    public string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return "registered email";

        var parts = email.Split('@');
        string name = parts[0];
        string domain = parts[1];

        string maskedName = name.Length <= 2 ? name[0] + "***" : name[..2] + "***";
        return $"{maskedName}@{domain}";
    }

    private string HashCode(string input)
    {
        using var hmac = new HMACSHA256(_otpPepperKey);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input + "SEPS_MFA_SALT_2026"));
        return Convert.ToHexString(hash);
    }

    private string ToBase32String(byte[] bytes)
    {
        var sb = new StringBuilder();
        int currentByte = 0;
        int bitsRemaining = 8;
        int mask = 0;

        for (int i = 0; i < bytes.Length;)
        {
            if (bitsRemaining > 5)
            {
                mask = 0x1F << (bitsRemaining - 5);
                int val = (bytes[i] & mask) >> (bitsRemaining - 5);
                sb.Append(Base32Alphabet[val]);
                bitsRemaining -= 5;
            }
            else if (bitsRemaining == 5)
            {
                int val = bytes[i] & 0x1F;
                sb.Append(Base32Alphabet[val]);
                i++;
                bitsRemaining = 8;
            }
            else
            {
                int bitsFromFirst = bitsRemaining;
                int val = (bytes[i] & ((1 << bitsFromFirst) - 1)) << (5 - bitsFromFirst);
                i++;
                if (i < bytes.Length)
                {
                    int bitsFromSecond = 5 - bitsFromFirst;
                    val |= (bytes[i] >> (8 - bitsFromSecond)) & ((1 << bitsFromSecond) - 1);
                    bitsRemaining = 8 - bitsFromSecond;
                }
                else
                {
                    bitsRemaining = 0;
                }
                sb.Append(Base32Alphabet[val]);
            }
        }

        return sb.ToString();
    }

    private byte[] FromBase32String(string base32)
    {
        base32 = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        int buffer = 0;
        int bitsInBuffer = 0;

        foreach (char c in base32)
        {
            int val = Base32Alphabet.IndexOf(c);
            if (val < 0) throw new ArgumentException($"Invalid Base32 character: {c}");

            buffer = (buffer << 5) | val;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                bytes.Add((byte)((buffer >> bitsInBuffer) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
