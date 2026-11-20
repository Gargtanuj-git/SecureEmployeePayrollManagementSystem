using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize]
public class DesignationsController : Controller
{
    private readonly DesignationService _designationService;

    public DesignationsController(DesignationService designationService)
    {
        _designationService = designationService;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ============================================================
    // GET: /Designations
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var designations = await _designationService.GetAllDesignationsAsync(search);
        ViewBag.Search = search;
        return View(designations);
    }

    // ============================================================
    // GET: /Designations/Details/5
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var designation = await _designationService.GetByIdAsync(id);
        if (designation == null)
        {
            return NotFound();
        }

        return View(designation);
    }

    // ============================================================
    // GET: /Designations/Create
    // ============================================================
    [HttpGet]
    [Authorize(Policy = "HROrAdmin")]
    public IActionResult Create()
    {
        return View(new Designation { JobLevel = "Mid-Level" });
    }

    // ============================================================
    // POST: /Designations/Create
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Create(Designation model)
    {
        if (string.IsNullOrWhiteSpace(model.DesignationName))
        {
            ModelState.AddModelError(nameof(model.DesignationName), "Designation Name is required.");
        }
        if (string.IsNullOrWhiteSpace(model.JobLevel))
        {
            ModelState.AddModelError(nameof(model.JobLevel), "Job Level is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error, id) = await _designationService.CreateAsync(
            model,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Designation '{model.DesignationName}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // GET: /Designations/Edit/5
    // ============================================================
    [HttpGet]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var designation = await _designationService.GetByIdAsync(id);
        if (designation == null)
        {
            return NotFound();
        }

        return View(designation);
    }

    // ============================================================
    // POST: /Designations/Edit
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "HROrAdmin")]
    public async Task<IActionResult> Edit(Designation model)
    {
        if (string.IsNullOrWhiteSpace(model.DesignationName))
        {
            ModelState.AddModelError(nameof(model.DesignationName), "Designation Name is required.");
        }
        if (string.IsNullOrWhiteSpace(model.JobLevel))
        {
            ModelState.AddModelError(nameof(model.JobLevel), "Job Level is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error) = await _designationService.UpdateAsync(
            model,
            GetCurrentUserId(),
            GetIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Designation '{model.DesignationName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
