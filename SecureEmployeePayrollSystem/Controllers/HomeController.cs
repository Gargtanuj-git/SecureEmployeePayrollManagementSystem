using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;
using SecureEmployeePayrollSystem.ViewModels;

namespace SecureEmployeePayrollSystem.Controllers;

public class HomeController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly IContactService _contactService;
    private readonly IFeedbackService _feedbackService;

    public HomeController(
        DashboardService dashboardService,
        IContactService contactService,
        IFeedbackService feedbackService)
    {
        _dashboardService = dashboardService;
        _contactService = contactService;
        _feedbackService = feedbackService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        // If user is authenticated, route them to their secure enterprise dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            return await GetDashboardViewAsync();
        }

        // Public visitor: render the public homepage
        var featuredFeedback = await _feedbackService.GetFeaturedFeedbackAsync();
        var model = new HomePublicViewModel
        {
            FeaturedFeedback = featuredFeedback
        };
        return View("PublicHome", model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Public()
    {
        var featuredFeedback = await _feedbackService.GetFeaturedFeedbackAsync();
        var model = new HomePublicViewModel
        {
            FeaturedFeedback = featuredFeedback
        };
        return View("PublicHome", model);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        return await GetDashboardViewAsync();
    }

    private async Task<IActionResult> GetDashboardViewAsync()
    {
        int? employeeId = null;
        var empClaim = User.FindFirst("EmployeeId")?.Value;
        if (int.TryParse(empClaim, out int parsedId) && parsedId > 0)
        {
            employeeId = parsedId;
        }

        bool isManager = User.IsInRole("Admin") || 
                         User.IsInRole("HR Manager") || 
                         User.IsInRole("Payroll Manager");

        var model = await _dashboardService.GetDashboardDataAsync(employeeId, isManager);
        return View("Index", model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitContact([FromForm] ContactMessage form)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = false, message = errors });
            }
            TempData["ContactError"] = errors;
            return RedirectToAction(nameof(Public));
        }

        form.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        bool success = await _contactService.SubmitContactMessageAsync(form);

        if (success)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Thank you! Your inquiry has been received. A support specialist will respond within 24 hours." });
            }
            TempData["ContactSuccess"] = "Thank you! Your inquiry has been received. A support specialist will respond within 24 hours.";
        }
        else
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = false, message = "Unable to submit your message at this time. Please try again later." });
            }
            TempData["ContactError"] = "Unable to submit your message at this time. Please try again later.";
        }

        return RedirectToAction(nameof(Public));
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback([FromForm] UserFeedback form)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = false, message = errors });
            }
            TempData["FeedbackError"] = errors;
            return RedirectToAction(nameof(Public));
        }

        bool success = await _feedbackService.SubmitFeedbackAsync(form);

        if (success)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Thank you for your valuable feedback! Your submission helps us continuously enhance SEPS." });
            }
            TempData["FeedbackSuccess"] = "Thank you for your valuable feedback! Your submission helps us continuously enhance SEPS.";
        }
        else
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = false, message = "Unable to save your feedback. Please try again later." });
            }
            TempData["FeedbackError"] = "Unable to save your feedback. Please try again later.";
        }

        return RedirectToAction(nameof(Public));
    }
}
