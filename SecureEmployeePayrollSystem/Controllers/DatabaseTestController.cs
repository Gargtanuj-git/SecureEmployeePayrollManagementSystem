using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureEmployeePayrollSystem.Data;

namespace SecureEmployeePayrollSystem.Controllers;

public class DatabaseTestController : Controller
{
    private readonly PayrollDbContext _context;

    public DatabaseTestController(PayrollDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            bool connected = await _context.Database.CanConnectAsync();

            if (connected)
            {
                return Content("DATABASE CONNECTION SUCCESS - SecureEmployeePayrollDB");
            }

            return Content("DATABASE CONNECTION FAILED");
        }
        catch (Exception ex)
        {
            return Content("DATABASE ERROR: " + ex.Message);
        }
    }
}