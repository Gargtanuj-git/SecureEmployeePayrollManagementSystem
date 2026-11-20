using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;
using SecureEmployeePayrollSystem.ViewModels;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize]
public class EmployeesController : Controller
{
    private readonly EmployeeService _employeeService;
    private readonly PayrollDbContext _context;
    private readonly ILogger<EmployeesController> _logger;
    private readonly IIntegrityAssignmentService? _integrity;

    public EmployeesController(
        EmployeeService employeeService,
        PayrollDbContext context,
        ILogger<EmployeesController> logger,
        IIntegrityAssignmentService? integrity = null)
    {
        _employeeService = employeeService;
        _context = context;
        _logger = logger;
        _integrity = integrity;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private int? GetCurrentEmployeeId()
    {
        var idClaim = User.FindFirst("EmployeeId")?.Value;
        return int.TryParse(idClaim, out int id) && id > 0 ? id : null;
    }

    private string? GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private async Task PopulateDropdownsAsync(EmployeeFormViewModel model)
    {
        var departments = await _context.Departments
            .Where(d => d.DepartmentStatus == "Active" || d.DepartmentId == model.DepartmentId)
            .OrderBy(d => d.DepartmentName)
            .ToListAsync();

        var designations = await _context.Designations
            .Where(d => d.DesignationId == model.DesignationId || true)
            .OrderBy(d => d.DesignationName)
            .ToListAsync();

        var structures = await _context.SalaryStructures
            .OrderBy(s => s.StructureName)
            .ToListAsync();

        model.DepartmentOptions = new SelectList(departments, "DepartmentId", "DepartmentName", model.DepartmentId);
        model.DesignationOptions = new SelectList(designations, "DesignationId", "DesignationName", model.DesignationId);
        model.SalaryStructureOptions = new SelectList(structures, "SalaryStructureId", "StructureName", model.SalaryStructureId);
    }

    // ============================================================
    // EMPLOYEE DIRECTORY
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? departmentId,
        string? status,
        int page = 1,
        string sortColumn = "Name",
        bool sortAsc = true)
    {
        // If user is Employee-only and has an Employee record, optionally allow browsing or view profile
        var (items, totalCount) = await _employeeService.GetEmployeesAsync(
            search,
            departmentId,
            status,
            page,
            10,
            sortColumn,
            sortAsc);

        var departments = await _context.Departments
            .OrderBy(d => d.DepartmentName)
            .ToListAsync();

        var viewModel = new EmployeeListViewModel
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = 10,
            Search = search,
            DepartmentId = departmentId,
            Status = status,
            SortColumn = sortColumn,
            SortAsc = sortAsc,
            Departments = departments
        };

        return View(viewModel);
    }

    // ============================================================
    // EMPLOYEE DETAILS / PROFILE
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        // Authorization check: Employees can only view their own profile
        if (User.IsInRole("Employee") && !User.IsInRole("Admin") && !User.IsInRole("HR Manager") && !User.IsInRole("Payroll Manager"))
        {
            var myEmpId = GetCurrentEmployeeId();
            if (myEmpId == null || myEmpId.Value != id)
            {
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        if (_integrity != null)
        {
            var (readAllowed, readReason, _, _) = await _integrity.CheckReadAccessAsync(GetCurrentUserId(), "Employee", id, GetIpAddress());
            if (!readAllowed)
            {
                TempData["ErrorMessage"] = $"Biba Integrity Violation (No Read Down): {readReason}";
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        var employee = await _employeeService.GetEmployeeByIdAsync(id);
        if (employee == null)
        {
            return NotFound();
        }

        return View(employee);
    }

    // ============================================================
    // CREATE EMPLOYEE (HR / Admin)
    // ============================================================
    [HttpGet]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Create()
    {
        var model = new EmployeeFormViewModel();
        await PopulateDropdownsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Create(EmployeeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        var employee = new Employee
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            Email = model.Email.Trim().ToLowerInvariant(),
            Phone = model.Phone.Trim(),
            Address = model.Address.Trim(),
            HireDate = model.HireDate,
            EmploymentStatus = model.EmploymentStatus,
            DepartmentId = model.DepartmentId,
            DesignationId = model.DesignationId,
            SalaryStructureId = model.SalaryStructureId
        };

        var (success, error, employeeId) = await _employeeService.CreateEmployeeAsync(
            employee,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Employee {employee.FirstName} {employee.LastName} created successfully (ID: #{employeeId}).";
        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    // ============================================================
    // EDIT EMPLOYEE (HR / Admin)
    // ============================================================
    [HttpGet]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null)
        {
            return NotFound();
        }

        var model = new EmployeeFormViewModel
        {
            EmployeeId = employee.EmployeeId,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            DateOfBirth = employee.DateOfBirth,
            Gender = employee.Gender,
            Email = employee.Email,
            Phone = employee.Phone,
            Address = employee.Address,
            HireDate = employee.HireDate,
            EmploymentStatus = employee.EmploymentStatus,
            DepartmentId = employee.DepartmentId,
            DesignationId = employee.DesignationId,
            SalaryStructureId = employee.SalaryStructureId
        };

        await PopulateDropdownsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Edit(EmployeeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        var employee = new Employee
        {
            EmployeeId = model.EmployeeId,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            Email = model.Email.Trim().ToLowerInvariant(),
            Phone = model.Phone.Trim(),
            Address = model.Address.Trim(),
            HireDate = model.HireDate,
            EmploymentStatus = model.EmploymentStatus,
            DepartmentId = model.DepartmentId,
            DesignationId = model.DesignationId,
            SalaryStructureId = model.SalaryStructureId
        };

        var (success, error) = await _employeeService.UpdateEmployeeAsync(
            employee,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Employee {employee.FirstName} {employee.LastName} updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.EmployeeId });
    }

    // ============================================================
    // TOGGLE STATUS (HR / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var (success, error) = await _employeeService.ToggleStatusAsync(
            id,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "Employee status updated.";
        }

        return RedirectToAction(nameof(Index));
    }
}