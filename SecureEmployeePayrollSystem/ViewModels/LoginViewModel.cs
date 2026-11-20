using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SecureEmployeePayrollSystem.ViewModels;

public class LoginViewModel : IValidatableObject
{
    private static readonly Regex ValidUsernameRegex = new(@"^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);
    private static readonly EmailAddressAttribute EmailValidator = new();

    [Required(ErrorMessage = "Please enter your username or email.")]
    [StringLength(150, ErrorMessage = "Username or email cannot exceed 150 characters.")]
    [Display(Name = "Username or Email")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public string GetNormalizedIdentifier()
    {
        return UsernameOrEmail?.Trim() ?? string.Empty;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 1. Validate UsernameOrEmail
        if (string.IsNullOrWhiteSpace(UsernameOrEmail))
        {
            yield return new ValidationResult(
                "Please enter your username or email.",
                new[] { nameof(UsernameOrEmail) });
        }
        else
        {
            string trimmed = UsernameOrEmail.Trim();

            if (trimmed.Length > 150)
            {
                yield return new ValidationResult(
                    "Username or email cannot exceed 150 characters.",
                    new[] { nameof(UsernameOrEmail) });
            }

            // Reject control characters in username/email
            if (trimmed.Any(c => char.IsControl(c)))
            {
                yield return new ValidationResult(
                    "Username or email contains invalid control characters.",
                    new[] { nameof(UsernameOrEmail) });
            }
            else if (trimmed.Contains('@'))
            {
                // Validate email syntax
                if (!EmailValidator.IsValid(trimmed))
                {
                    yield return new ValidationResult(
                        "Please enter a valid email address.",
                        new[] { nameof(UsernameOrEmail) });
                }
            }
            else
            {
                // Validate username syntax (letters, digits, dots, underscores, hyphens)
                if (!ValidUsernameRegex.IsMatch(trimmed))
                {
                    yield return new ValidationResult(
                        "Username can only contain letters, numbers, periods, underscores, and hyphens.",
                        new[] { nameof(UsernameOrEmail) });
                }
            }
        }

        // 2. Validate Password
        if (string.IsNullOrEmpty(Password))
        {
            yield return new ValidationResult(
                "Please enter your password.",
                new[] { nameof(Password) });
        }
        else if (string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult(
                "Password cannot consist solely of whitespace.",
                new[] { nameof(Password) });
        }
        else
        {
            if (Password.Length > 128)
            {
                yield return new ValidationResult(
                    "Password cannot exceed 128 characters.",
                    new[] { nameof(Password) });
            }

            // Reject control characters (allow standard printable chars)
            if (Password.Any(c => char.IsControl(c) && c != '\t'))
            {
                yield return new ValidationResult(
                    "Password contains invalid control characters.",
                    new[] { nameof(Password) });
            }
        }
    }
}