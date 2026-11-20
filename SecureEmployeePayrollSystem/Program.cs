using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;
using SecureEmployeePayrollSystem.Authorization;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;
using SecureEmployeePayrollSystem.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<PayrollDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));

// Data Protection: Persist encryption keys across server restarts
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
if (!Directory.Exists(keysFolder))
{
    Directory.CreateDirectory(keysFolder);
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("SEPS");

// Distributed cache & Session for safe enrollment workflows
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Layered Rate Limiting Policies for Brute-Force Defense
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "<!DOCTYPE html><html><head><title>Too Many Requests</title></head><body style='font-family: Arial, sans-serif; padding: 40px; text-align: center; color: #1e293b; background: #f8fafc;'>" +
            "<div style='max-width: 500px; margin: 0 auto; background: #fff; padding: 32px; border-radius: 12px; border: 1px solid #e2e8f0; box-shadow: 0 4px 12px rgba(0,0,0,0.05);'>" +
            "<h1 style='color: #dc2626; font-size: 22px; margin-bottom: 8px;'>429 — Too Many Requests</h1>" +
            "<p style='color: #64748b; font-size: 14px; line-height: 1.5;'>Security rate limit reached. Please wait a moment before attempting another request.</p>" +
            "<a href='/Account/Login' style='display: inline-block; margin-top: 16px; padding: 8px 16px; background: #1e3a8a; color: #fff; text-decoration: none; border-radius: 6px; font-weight: 600; font-size: 13px;'>Return to Sign In</a>" +
            "</div></body></html>", cancellationToken: token);
    };

    options.AddPolicy("LoginLimiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("MfaLimiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("OtpResendLimiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(2),
            QueueLimit = 0
        });
    });

    options.AddPolicy("PasswordResetLimiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0
        });
    });

    options.AddPolicy("EmailVerificationLimiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        });
    });
});

builder.Services.AddScoped<AccountAuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<DepartmentService>();
builder.Services.AddScoped<DesignationService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<SalaryStructureService>();
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<MfaService>();
builder.Services.AddScoped<IIntegrityPolicyService, IntegrityPolicyService>();
builder.Services.AddScoped<IIntegrityAssignmentService, IntegrityAssignmentService>();
builder.Services.AddScoped<IAuthorizationHandler, IntegrityAuthorizationHandler>();
builder.Services.AddScoped<IIntegrityVerificationService, IntegrityVerificationService>();
builder.Services.AddScoped<ISeparationOfDutiesService, SeparationOfDutiesService>();
builder.Services.AddScoped<ITransformationProcedureService, TransformationProcedureService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultSignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";

        options.AccessDeniedPath =
            "/Account/AccessDenied";

        options.Cookie.Name =
            "SecureEmployeePayroll.Auth";

        options.Cookie.HttpOnly = true;

        options.Cookie.SameSite =
            SameSiteMode.Lax;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;

        options.ExpireTimeSpan =
            TimeSpan.FromMinutes(60);

        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("HROrAdmin", policy => policy.RequireRole("Admin", "HR Manager"));
    options.AddPolicy("PayrollOrAdmin", policy => policy.RequireRole("Admin", "Payroll Manager"));
    options.AddPolicy("StaffOrAdmin", policy => policy.RequireRole("Admin", "HR Manager", "Payroll Manager"));
    options.AddPolicy("BibaHighlyTrustedWrite", policy =>
        policy.Requirements.Add(new IntegrityRequirement(IntegrityLevel.HighlyTrusted, BibaAction.Write)));
    options.AddPolicy("BibaTrustedWrite", policy =>
        policy.Requirements.Add(new IntegrityRequirement(IntegrityLevel.Trusted, BibaAction.Write)));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Enterprise Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

    await next();
});

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();
app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}");

app.Run();