using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SecureEmployeePayrollSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    public IActionResult Index()
    {
        return Content(
            $"ADMIN ACCESS GRANTED - Logged in as {User.Identity?.Name}");
    }
}