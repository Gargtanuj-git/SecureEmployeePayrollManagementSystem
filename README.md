# Secure Employee Payroll Management System (SEPS)

Enterprise-grade, secure, multi-tier Employee and Payroll Management System built on **ASP.NET Core 10.0**, **Entity Framework Core**, and **Microsoft SQL Server**. SEPS incorporates formal cryptographic authentication, Role-Based Access Control (RBAC), the **Biba Integrity Model** (No Read Down / No Write Up), and the **Clark-Wilson Integrity Model** (Constrained Data Items, Transformation Procedures, and Separation of Duties).

---

## Key Highlights & Capabilities

- **Role-Based Access Control (RBAC)**: Segregated roles for **Admin**, **HR Manager**, **Payroll Manager**, and **Employee** with strict policy boundaries.
- **Formal Integrity Models**:
  - **Biba Strict Integrity**: Prevents unauthorized modification of high-integrity objects by lower-integrity subjects (No Write Up) and prevents lower-integrity data pollution into high-integrity processes (No Read Down).
  - **Clark-Wilson Integrity**: Guarantees accounting consistency through Constrained Data Items (CDIs), audited Transformation Procedures (TPs), Integrity Verification Procedures (IVPs), and strict Separation of Duties (e.g. self-approval prevention on payroll disbursement).
- **Multi-Factor Authentication (MFA)**: TOTP authenticator app support, Email OTP delivery, single-use recovery codes, and brute-force lockout safeguards.
- **Modern Responsive UI/UX**: Premium animated landing page with scroll-reveal cards, an interactive 10-entry feedback carousel with live submission, and high-DPI dashboard metrics.
- **Enterprise Audit Trail**: Cryptographically structured audit logging capturing actor, action, table, record ID, IP address, and status.

---

## Role Matrix & Default Test Credentials

| Username | Role | Password | Description & Permissions |
| :--- | :--- | :--- | :--- |
| `aarav.mehta` | **Admin** | `Admin@123` | System oversight, user accounts, roles & privileges, audit trail, security classifications, system configuration. *(Requires MFA)* |
| `riya.sharma` | **HR Manager** | `Hr@123` | Employee lifecycle management, department & designation directories, leave approval queue, attendance monitoring. |
| `vikram.singh` | **Payroll Manager** | `Payroll@123` | Compensation grading, monthly payroll processing runs, salary disbursement, payslip generation. |
| `kabir.verma` | **Employee** | `Emp@123` | Self-service portal: personal profile view, attendance clock in/out, leave application, payslip review. |

---

## Architecture & Technology Stack

- **Framework**: .NET 10.0 / ASP.NET Core 10.0 MVC
- **ORM**: Entity Framework Core 9 / 10 with SQL Server Provider
- **Database**: Microsoft SQL Server Express (`SecureEmployeePayrollDB`)
- **Authentication**: Cookie Authentication with ASP.NET Identity Password Hasher
- **Security Protections**: Enterprise Security Headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`), Rate Limiting (Fixed Window Limiters for Login and MFA), Anti-Forgery Tokens (`ValidateAntiForgeryToken`), and CSV Injection Sanitization.

---

## Database Setup & Configuration

1. **Prerequisites**:
   - .NET 10.0 SDK
   - Microsoft SQL Server (2019+ or SQL Server Express)
   - PowerShell 7+ or Windows PowerShell 5.1

2. **Connection Configuration**:
   Copy `SecureEmployeePayrollSystem/appsettings.example.json` to `SecureEmployeePayrollSystem/appsettings.json` and adjust your SQL Server instance name:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SecureEmployeePayrollDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
     }
   }
   ```

3. **Database Schema & Migrations**:
   The SQL schema and formal integrity migrations are located in:
   - `SecureEmployeePayrollSystem/Data/Biba_Integrity_Migration.sql`
   - `SecureEmployeePayrollSystem/Data/ClarkWilson_Migration.sql`

---

## Build and Execution

Because Windows Code Integrity / Smart App Control validates local assemblies, use the automated signing script after building:

```powershell
# 1. Build the solution
dotnet build .\SecureEmployeePayrollSystem.slnx

# 2. Auto-sign the compiled binaries with the local development certificate
powershell -ExecutionPolicy Bypass -File .\sign.ps1

# 3. Launch the web application
dotnet run --project .\SecureEmployeePayrollSystem\SecureEmployeePayrollSystem.csproj --no-build
```

The application will listen on:
- **HTTP**: `http://localhost:5039`

---

## Automated Test Suites

The repository includes a comprehensive 62-test security and functional test harness:

```powershell
# Run the complete test suite
dotnet run --project .\SecureEmployeePayrollSystem.Tests\SecureEmployeePayrollSystem.Tests.csproj --no-build
```

The suite validates:
1. Multi-role authentication & credential verification.
2. Brute-force lockout and 30-second security backoff.
3. Anti-tamper cryptographic token generators.
4. Biba Integrity Rules (No Read Down, No Write Up, Object Promotions).
5. Clark-Wilson Integrity Procedures (Transformation Procedures, IVP discrepancy checks, Separation of Duties).
6. End-to-end CRUD operations for Departments, Designations, Employees, Attendance, Leaves, Payroll, and Reports.

---

## License

This project is licensed under the MIT License.
