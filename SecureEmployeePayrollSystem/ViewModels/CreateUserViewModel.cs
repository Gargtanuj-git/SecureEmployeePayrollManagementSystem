using System.ComponentModel.DataAnnotations;

namespace SecureEmployeePayrollSystem.ViewModels;

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Employee (Optional)")]
    public int? EmployeeId { get; set; }

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username can only contain alphanumeric characters, dots, underscores, and dashes.")]
    public string Username { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [Display(Name = "Phone Number (Optional)")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Department (Optional)")]
    public int? DepartmentId { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role assignment is required.")]
    [Display(Name = "Role")]
    public int RoleId { get; set; }

    [Display(Name = "Status")]
    public string AccountStatus { get; set; } = "Active";
}
