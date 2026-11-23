using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize(Policy = "StaffOrAdmin")]
public class SalaryStructureController : Controller
{
    private readonly SalaryStructureService _salaryStructureService;
    private readonly IIntegrityAssignmentService? _integrity;

    public SalaryStructureController(
        SalaryStructureService salaryStructureService,
        IIntegrityAssignmentService? integrity = null)
    {
        _salaryStructureService = salaryStructureService;
        _integrity = integrity;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ============================================================
    // DIRECTORY
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var structures = await _salaryStructureService.GetAllStructuresAsync();
        return View(structures);
    }

    // ============================================================
    // DETAILS
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (_integrity != null)
        {
            var (readAllowed, readReason, _, _) = await _integrity.CheckReadAccessAsync(GetCurrentUserId(), "SalaryStructure", id, GetIpAddress());
            if (!readAllowed)
            {
                TempData["ErrorMessage"] = $"Biba Integrity Violation (No Read Down): {readReason}";
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        var structure = await _salaryStructureService.GetByIdAsync(id);
        if (structure == null)
        {
            return NotFound();
        }

        return View(structure);
    }

    // ============================================================
    // CREATE (Payroll / Admin)
    // ============================================================
    [HttpGet]
    [Authorize(Policy = "PayrollOrAdmin")]
    public IActionResult Create()
    {
        var model = new SalaryStructure
        {
            Status = "Active",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today),
            BasicSalary = 50000m
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PayrollOrAdmin")]
    public async Task<IActionResult> Create(SalaryStructure model)
    {
        if (string.IsNullOrWhiteSpace(model.StructureName))
        {
            ModelState.AddModelError(nameof(model.StructureName), "Structure Name is required.");
        }
        if (model.BasicSalary <= 0)
        {
            ModelState.AddModelError(nameof(model.BasicSalary), "Basic Salary must be greater than zero.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error, id) = await _salaryStructureService.CreateStructureAsync(
            model,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Salary Structure '{model.StructureName}' created successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ============================================================
    // EDIT (Payroll / Admin)
    // ============================================================
    [HttpGet]
    [Authorize(Policy = "PayrollOrAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var structure = await _salaryStructureService.GetByIdAsync(id);
        if (structure == null)
        {
            return NotFound();
        }

        return View(structure);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PayrollOrAdmin")]
    public async Task<IActionResult> Edit(SalaryStructure model)
    {
        if (string.IsNullOrWhiteSpace(model.StructureName))
        {
            ModelState.AddModelError(nameof(model.StructureName), "Structure Name is required.");
        }
        if (model.BasicSalary <= 0)
        {
            ModelState.AddModelError(nameof(model.BasicSalary), "Basic Salary must be greater than zero.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error) = await _salaryStructureService.UpdateStructureAsync(
            model,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Salary Structure '{model.StructureName}' updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.SalaryStructureId });
    }

    // ============================================================
    // ADD COMPONENT (Payroll / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PayrollOrAdmin")]
    public async Task<IActionResult> AddComponent(
        int salaryStructureId,
        string componentName,
        string componentType,
        string calculationType,
        decimal? percentage,
        decimal? fixedAmount)
    {
        var component = new SalaryComponent
        {
            SalaryStructureId = salaryStructureId,
            ComponentName = componentName.Trim(),
            ComponentType = componentType,
            CalculationType = calculationType,
            Percentage = calculationType == "Percentage" ? percentage : null,
            FixedAmount = calculationType == "Fixed" ? fixedAmount : null
        };

        var (success, error) = await _salaryStructureService.AddComponentAsync(
            component,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = $"Added component '{componentName}' to structure.";
        }

        return RedirectToAction(nameof(Details), new { id = salaryStructureId });
    }

    // ============================================================
    // DELETE COMPONENT (Payroll / Admin)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PayrollOrAdmin")]
    public async Task<IActionResult> DeleteComponent(int componentId, int salaryStructureId)
    {
        var (success, error) = await _salaryStructureService.DeleteComponentAsync(
            componentId,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "Salary component removed.";
        }

        return RedirectToAction(nameof(Details), new { id = salaryStructureId });
    }
}
