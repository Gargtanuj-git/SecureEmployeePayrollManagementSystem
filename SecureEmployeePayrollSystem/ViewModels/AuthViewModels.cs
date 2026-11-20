using System.ComponentModel.DataAnnotations;

namespace SecureEmployeePayrollSystem.ViewModels;

public class RegisterViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Employee ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "A valid Employee ID is required.")]
    [Display(Name = "Employee ID")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Registered work email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    [Display(Name = "Work Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username can only contain letters, numbers, dots, underscores and hyphens.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password confirmation is required.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult("Password cannot be empty or consist only of whitespace.", new[] { nameof(Password) });
        }

        foreach (char c in Password)
        {
            if (char.IsControl(c))
            {
                yield return new ValidationResult("Password contains prohibited control characters.", new[] { nameof(Password) });
                break;
            }
        }

        foreach (char c in Username)
        {
            if (char.IsControl(c))
            {
                yield return new ValidationResult("Username contains prohibited control characters.", new[] { nameof(Username) });
                break;
            }
        }
    }
}

public class ForgotPasswordViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Please enter your username or registered email.")]
    [StringLength(150, ErrorMessage = "Identifier cannot exceed 150 characters.")]
    [Display(Name = "Username or Email")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(UsernameOrEmail))
        {
            yield return new ValidationResult("Please enter your username or registered email.", new[] { nameof(UsernameOrEmail) });
        }

        foreach (char c in UsernameOrEmail)
        {
            if (char.IsControl(c))
            {
                yield return new ValidationResult("Identifier contains invalid control characters.", new[] { nameof(UsernameOrEmail) });
                break;
            }
        }
    }
}

public class ResetPasswordViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(80, ErrorMessage = "Username cannot exceed 80 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reset token is required.")]
    [StringLength(256, ErrorMessage = "Reset token exceeds maximum permitted length.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(128, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password confirmation is required.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            yield return new ValidationResult("New password cannot be empty or consist only of whitespace.", new[] { nameof(NewPassword) });
        }

        foreach (char c in NewPassword)
        {
            if (char.IsControl(c))
            {
                yield return new ValidationResult("Password contains prohibited control characters.", new[] { nameof(NewPassword) });
                break;
            }
        }
    }
}

public class VerifyEmailViewModel
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Username { get; set; }
}

public class MfaChallengeViewModel
{
    [Required]
    [StringLength(256)]
    public string ChallengeToken { get; set; } = string.Empty;

    public string Method { get; set; } = "TOTP"; // "TOTP", "EMAIL", "RECOVERY"

    [Required(ErrorMessage = "Please enter your 6-digit code or emergency recovery code.")]
    [StringLength(32, ErrorMessage = "Security code is invalid.")]
    [Display(Name = "Security Code")]
    public string Code { get; set; } = string.Empty;

    public string? MaskedEmail { get; set; }

    public bool HasTotp { get; set; }

    public bool HasEmail { get; set; }

    public bool HasRecovery { get; set; }

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class MfaEnrollViewModel
{
    public string? ChallengeToken { get; set; }

    public string SecretKey { get; set; } = string.Empty;

    public string QrCodeDataUri { get; set; } = string.Empty;

    public string ManualEntryKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the 6-digit verification code from your authenticator app.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Verification code must be exactly 6 digits.")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Code must be 6 digits.")]
    [Display(Name = "Authenticator Code")]
    public string VerificationCode { get; set; } = string.Empty;

    public List<string> RecoveryCodes { get; set; } = new List<string>();

    public string? EmailAddress { get; set; }

    public string? MaskedEmail { get; set; }

    public string SelectedMethod { get; set; } = "TOTP";
}
