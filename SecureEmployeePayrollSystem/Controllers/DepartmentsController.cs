using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize]
public class DepartmentsController : Controller
{
    private readonly DepartmentService _departmentService;

    public DepartmentsController(DepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ============================================================
    // GET: /Departments
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var departments = await _departmentService.GetAllDepartmentsAsync(search);
        ViewBag.Search = search;
        return View(departments);
    }

    // ============================================================
    // GET: /Departments/Details/5
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department == null)
        {
            return NotFound();
        }

        return View(department);
    }

    // ============================================================
    // GET: /Departments/Create
    // ============================================================
    [HttpGet]
    [Authorize(Policy = "HROrAdmin")]
    public IActionResult Create()
    {
        return View(new Department { DepartmentStatus = "Active" });
    }

    // ============================================================
    // POST: /Departments/Create
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Create(Department model)
    {
        if (string.IsNullOrWhiteSpace(model.DepartmentStatus))
        {
            model.DepartmentStatus = "Active";
            ModelState.Remove(nameof(model.DepartmentStatus));
        }

        if (string.IsNullOrWhiteSpace(model.DepartmentName))
        {
            ModelState.AddModelError(nameof(model.DepartmentName), "Department Name is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error, id) = await _departmentService.CreateAsync(
            model,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Department '{model.DepartmentName}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // GET: /Departments/Edit/5
    // ============================================================
    [HttpGet]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department == null)
        {
            return NotFound();
        }

        return View(department);
    }

    // ============================================================
    // POST: /Departments/Edit
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Edit(Department model)
    {
        if (string.IsNullOrWhiteSpace(model.DepartmentName))
        {
            ModelState.AddModelError(nameof(model.DepartmentName), "Department Name is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error) = await _departmentService.UpdateAsync(
            model,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Department '{model.DepartmentName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // POST: /Departments/ToggleStatus
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var (success, error) = await _departmentService.ToggleStatusAsync(
            id,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "Department status changed.";
        }

        return RedirectToAction(nameof(Index));
    }
}