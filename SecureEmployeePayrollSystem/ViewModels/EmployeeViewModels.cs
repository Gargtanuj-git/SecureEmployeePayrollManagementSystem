using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.ViewModels;

public class EmployeeListViewModel
{
    public List<Employee> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 10));
    public string? Search { get; set; }
    public int? DepartmentId { get; set; }
    public string? Status { get; set; }
    public string SortColumn { get; set; } = "Name";
    public bool SortAsc { get; set; } = true;
    public List<Department> Departments { get; set; } = new();
}

public class EmployeeFormViewModel
{
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Date of birth is required.")]
    [Display(Name = "Date of Birth")]
    [DataType(DataType.Date)]
    public DateOnly DateOfBirth { get; set; } = new DateOnly(1995, 1, 1);

    [Required(ErrorMessage = "Gender is required.")]
    public string Gender { get; set; } = "Male";

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Invalid phone number.")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(255)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hire date is required.")]
    [Display(Name = "Hire Date")]
    [DataType(DataType.Date)]
    public DateOnly HireDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required]
    [Display(Name = "Employment Status")]
    public string EmploymentStatus { get; set; } = "Active";

    [Required(ErrorMessage = "Department is required.")]
    [Display(Name = "Department")]
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "Designation is required.")]
    [Display(Name = "Designation")]
    public int DesignationId { get; set; }

    [Required(ErrorMessage = "Salary structure is required.")]
    [Display(Name = "Salary Grade / Structure")]
    public int SalaryStructureId { get; set; }

    public SelectList? DepartmentOptions { get; set; }
    public SelectList? DesignationOptions { get; set; }
    public SelectList? SalaryStructureOptions { get; set; }
}
