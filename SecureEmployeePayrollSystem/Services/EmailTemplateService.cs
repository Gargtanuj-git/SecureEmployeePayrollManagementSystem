using System;

namespace SecureEmployeePayrollSystem.Services;

public static class EmailTemplateService
{
    private const string PrimaryBrandColor = "#10b981";
    private const string DarkHeaderColor = "#0f172a";

    public static string BuildVerificationEmail(string username, string verificationUrl, int expiryHours = 24)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Verify Your SEPS Account</title>
</head>
<body style=""margin: 0; padding: 24px; background-color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b;"">
    <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 580px; background-color: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 12px rgba(15, 23, 42, 0.05);"">
        <!-- HEADER -->
        <tr>
            <td style=""background-color: {DarkHeaderColor}; padding: 28px 32px; text-align: left;"">
                <div style=""display: inline-block; padding: 6px 12px; background: rgba(16, 185, 129, 0.2); border: 1px solid rgba(16, 185, 129, 0.4); border-radius: 8px; color: #34d399; font-weight: 700; font-size: 13px; letter-spacing: 0.05em;"">
                    SEPS ENTERPRISE
                </div>
                <h1 style=""margin: 12px 0 0; color: #ffffff; font-size: 22px; font-weight: 700;"">
                    Verify Your Email Address
                </h1>
            </td>
        </tr>
        <!-- BODY -->
        <tr>
            <td style=""padding: 32px;"">
                <p style=""font-size: 15px; line-height: 1.6; margin: 0 0 16px; color: #334155;"">
                    Hello <strong>{System.Net.WebUtility.HtmlEncode(username)}</strong>,
                </p>
                <p style=""font-size: 14.5px; line-height: 1.6; margin: 0 0 24px; color: #475569;"">
                    Thank you for registering your self-service account with the <strong>Secure Employee Payroll System (SEPS)</strong>. To complete your activation and verify ownership of your registered email address, please select the button below:
                </p>

                <!-- CALL TO ACTION BUTTON -->
                <div style=""text-align: center; margin: 30px 0;"">
                    <a href=""{verificationUrl}"" target=""_blank"" style=""display: inline-block; background: linear-gradient(135deg, #10b981 0%, #059669 100%); color: #ffffff; text-decoration: none; padding: 14px 32px; font-size: 14.5px; font-weight: 600; border-radius: 10px; box-shadow: 0 4px 12px rgba(16, 185, 129, 0.35);"">
                        Verify and Activate Account
                    </a>
                </div>

                <p style=""font-size: 13px; line-height: 1.5; color: #64748b; margin: 24px 0 8px;"">
                    Or copy and paste this verification link into your browser:
                </p>
                <div style=""background: #f1f5f9; padding: 12px 14px; border-radius: 8px; font-family: monospace; font-size: 12px; color: #0f172a; word-break: break-all; border: 1px solid #cbd5e1;"">
                    {verificationUrl}
                </div>

                <div style=""margin-top: 24px; padding: 14px 16px; background: #fefce8; border-left: 4px solid #eab308; border-radius: 6px;"">
                    <p style=""margin: 0; font-size: 12.5px; color: #854d0e; line-height: 1.5;"">
                        <strong>Security Notice:</strong> This link is single-use and will expire in <strong>{expiryHours} hours</strong>. If you did not create this account, please ignore this email or report it to your HR department.
                    </p>
                </div>
            </td>
        </tr>
        <!-- FOOTER -->
        <tr>
            <td style=""background-color: #f8fafc; padding: 20px 32px; border-top: 1px solid #e2e8f0; font-size: 11.5px; color: #94a3b8; line-height: 1.5;"">
                <div>© {DateTime.UtcNow.Year} Secure Employee Payroll System • Confidential Corporate Transmission</div>
                <div style=""margin-top: 4px;"">Automated notification generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.</div>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    public static string BuildPasswordResetEmail(string username, string resetUrl, int expiryMinutes = 15)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Reset Your SEPS Password</title>
</head>
<body style=""margin: 0; padding: 24px; background-color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b;"">
    <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 580px; background-color: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 12px rgba(15, 23, 42, 0.05);"">
        <!-- HEADER -->
        <tr>
            <td style=""background-color: {DarkHeaderColor}; padding: 28px 32px; text-align: left;"">
                <div style=""display: inline-block; padding: 6px 12px; background: rgba(59, 130, 246, 0.2); border: 1px solid rgba(59, 130, 246, 0.4); border-radius: 8px; color: #60a5fa; font-weight: 700; font-size: 13px; letter-spacing: 0.05em;"">
                    SEPS SECURITY
                </div>
                <h1 style=""margin: 12px 0 0; color: #ffffff; font-size: 22px; font-weight: 700;"">
                    Password Reset Request
                </h1>
            </td>
        </tr>
        <!-- BODY -->
        <tr>
            <td style=""padding: 32px;"">
                <p style=""font-size: 15px; line-height: 1.6; margin: 0 0 16px; color: #334155;"">
                    Hello <strong>{System.Net.WebUtility.HtmlEncode(username)}</strong>,
                </p>
                <p style=""font-size: 14.5px; line-height: 1.6; margin: 0 0 24px; color: #475569;"">
                    A request was submitted to reset the password for your Secure Employee Payroll System account. Click the button below to establish a new password:
                </p>

                <!-- CALL TO ACTION BUTTON -->
                <div style=""text-align: center; margin: 30px 0;"">
                    <a href=""{resetUrl}"" target=""_blank"" style=""display: inline-block; background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); color: #ffffff; text-decoration: none; padding: 14px 32px; font-size: 14.5px; font-weight: 600; border-radius: 10px; box-shadow: 0 4px 12px rgba(37, 99, 235, 0.35);"">
                        Reset Account Password
                    </a>
                </div>

                <p style=""font-size: 13px; line-height: 1.5; color: #64748b; margin: 24px 0 8px;"">
                    If the button does not work, paste this link directly into your browser:
                </p>
                <div style=""background: #f1f5f9; padding: 12px 14px; border-radius: 8px; font-family: monospace; font-size: 12px; color: #0f172a; word-break: break-all; border: 1px solid #cbd5e1;"">
                    {resetUrl}
                </div>

                <div style=""margin-top: 24px; padding: 14px 16px; background: #fee2e2; border-left: 4px solid #ef4444; border-radius: 6px;"">
                    <p style=""margin: 0; font-size: 12.5px; color: #991b1b; line-height: 1.5;"">
                        <strong>Urgent Security Advisory:</strong> This reset link is valid for <strong>{expiryMinutes} minutes</strong> and can only be used once. If you did not initiate this request, your account credentials may be compromised. Please contact your administrator immediately.
                    </p>
                </div>
            </td>
        </tr>
        <!-- FOOTER -->
        <tr>
            <td style=""background-color: #f8fafc; padding: 20px 32px; border-top: 1px solid #e2e8f0; font-size: 11.5px; color: #94a3b8; line-height: 1.5;"">
                <div>© {DateTime.UtcNow.Year} Secure Employee Payroll System • Automated Incident Response</div>
                <div style=""margin-top: 4px;"">Request processed at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.</div>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    public static string BuildPasswordChangedNotification(string username, DateTime changeTimeUtc, string? ipAddress)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>SEPS Password Changed Successfully</title>
</head>
<body style=""margin: 0; padding: 24px; background-color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b;"">
    <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 580px; background-color: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 12px rgba(15, 23, 42, 0.05);"">
        <tr>
            <td style=""background-color: {DarkHeaderColor}; padding: 24px 32px; text-align: left;"">
                <div style=""display: inline-block; padding: 6px 12px; background: rgba(16, 185, 129, 0.2); border: 1px solid rgba(16, 185, 129, 0.4); border-radius: 8px; color: #34d399; font-weight: 700; font-size: 13px;"">
                    SEPS SECURITY ALERT
                </div>
                <h1 style=""margin: 12px 0 0; color: #ffffff; font-size: 20px; font-weight: 700;"">
                    Password Successfully Updated
                </h1>
            </td>
        </tr>
        <tr>
            <td style=""padding: 32px;"">
                <p style=""font-size: 15px; line-height: 1.6; margin: 0 0 16px; color: #334155;"">
                    Hello <strong>{System.Net.WebUtility.HtmlEncode(username)}</strong>,
                </p>
                <p style=""font-size: 14px; line-height: 1.6; margin: 0 0 20px; color: #475569;"">
                    This notification confirms that the password for your Secure Employee Payroll System account was changed on <strong>{changeTimeUtc:yyyy-MM-dd HH:mm:ss} UTC</strong>.
                </p>
                <div style=""background: #f1f5f9; padding: 14px 18px; border-radius: 8px; font-size: 13px; color: #334155; margin-bottom: 24px;"">
                    <div><strong>Request IP:</strong> {ipAddress ?? "Unknown"}</div>
                    <div style=""margin-top: 4px;""><strong>Status:</strong> Completed & Verified</div>
                </div>
                <p style=""font-size: 13px; line-height: 1.5; color: #dc2626; margin: 0;"">
                    <strong>Did not make this change?</strong> Immediately contact your system administrator or IT security team to lock down your account.
                </p>
            </td>
        </tr>
        <tr>
            <td style=""background-color: #f8fafc; padding: 18px 32px; border-top: 1px solid #e2e8f0; font-size: 11.5px; color: #94a3b8;"">
                © {DateTime.UtcNow.Year} Secure Employee Payroll System • Security Operations Center
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    public static string BuildMfaOtpEmail(string username, string otpCode, int expiryMinutes = 5, string? ipAddress = null)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>SEPS Security Verification Code</title>
</head>
<body style=""margin: 0; padding: 24px; background-color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b;"">
    <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 540px; background-color: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 12px rgba(15, 23, 42, 0.05);"">
        <tr>
            <td style=""background-color: {DarkHeaderColor}; padding: 24px 32px; text-align: left;"">
                <div style=""display: inline-block; padding: 4px 10px; background: rgba(16, 185, 129, 0.2); border: 1px solid rgba(16, 185, 129, 0.4); border-radius: 6px; color: #34d399; font-weight: 700; font-size: 12px;"">
                    MFA VERIFICATION
                </div>
                <h1 style=""margin: 10px 0 0; color: #ffffff; font-size: 20px; font-weight: 700;"">
                    Authentication Security Code
                </h1>
            </td>
        </tr>
        <tr>
            <td style=""padding: 32px;"">
                <p style=""font-size: 15px; margin: 0 0 12px; color: #334155;"">
                    Hello <strong>{System.Net.WebUtility.HtmlEncode(username)}</strong>,
                </p>
                <p style=""font-size: 14px; margin: 0 0 24px; color: #475569; line-height: 1.5;"">
                    A sign-in attempt was initiated for your payroll account. Enter the 6-digit one-time security code below to complete your authentication:
                </p>

                <div style=""text-align: center; margin: 28px 0; background: #f8fafc; padding: 20px; border-radius: 12px; border: 2px dashed #cbd5e1;"">
                    <span style=""font-size: 36px; font-weight: 800; letter-spacing: 8px; color: #0f766e; font-family: monospace;"">{otpCode}</span>
                </div>

                <p style=""font-size: 13px; color: #64748b; margin: 0 0 8px; line-height: 1.4;"">
                    This code will expire in <strong>{expiryMinutes} minutes</strong> and can only be used once.
                </p>
                <p style=""font-size: 12px; color: #94a3b8; margin: 16px 0 0; line-height: 1.4;"">
                    Client IP: {ipAddress ?? "Unknown"} • Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                </p>
            </td>
        </tr>
        <tr>
            <td style=""background-color: #f8fafc; padding: 18px 32px; border-top: 1px solid #e2e8f0; font-size: 11px; color: #94a3b8;"">
                © {DateTime.UtcNow.Year} Secure Employee Payroll System • Automated MFA Dispatch
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
