using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SecureEmployeePayrollSystem.Services;

public class EmailService : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration config,
        ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Email dispatch rejected: recipient email is missing or empty.");
            return false;
        }

        string? smtpHost = _config["Smtp:Host"];
        int smtpPort = int.TryParse(_config["Smtp:Port"], out int p) ? p : 587;
        string? smtpUser = _config["Smtp:Username"];
        string? smtpPass = _config["Smtp:Password"];
        bool enableSsl = !bool.TryParse(_config["Smtp:EnableSsl"], out bool ssl) || ssl;
        string fromAddress = _config["Smtp:FromAddress"] ?? "no-reply@seps-payroll.internal";
        string fromName = _config["Smtp:FromName"] ?? "SEPS Security";
        int timeoutMs = int.TryParse(_config["Smtp:TimeoutMs"], out int t) ? t : 10000;

        // Ensure mandatory SMTP configurations exist
        if (string.IsNullOrWhiteSpace(smtpHost) ||
            string.IsNullOrWhiteSpace(smtpUser) ||
            string.IsNullOrWhiteSpace(smtpPass))
        {
            _logger.LogWarning(
                "SMTP email delivery aborted: mandatory SMTP credentials not configured (Host: {HostConfigured}, User: {UserConfigured}).",
                !string.IsNullOrWhiteSpace(smtpHost),
                !string.IsNullOrWhiteSpace(smtpUser));
            return false;
        }

        try
        {
            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = enableSsl,
                Timeout = timeoutMs
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
            _logger.LogInformation(
                "SMTP email successfully delivered for subject '{Subject}' to domain '{RecipientDomain}'.",
                subject,
                GetDomain(toEmail));
            return true;
        }
        catch (Exception ex)
        {
            // Log technical failure without exposing passwords, credentials, or email body secrets
            _logger.LogError(
                ex,
                "SMTP email delivery failure for subject '{Subject}' to domain '{RecipientDomain}'. Host: {Host}:{Port}, SSL: {EnableSsl}.",
                subject,
                GetDomain(toEmail),
                smtpHost,
                smtpPort,
                enableSsl);
            return false;
        }
    }

    private static string GetDomain(string email)
    {
        int atIndex = email.IndexOf('@');
        return atIndex >= 0 && atIndex < email.Length - 1 ? email[(atIndex + 1)..] : "domain";
    }
}
