using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SecureEmployeePayrollSystem.Data;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public class ContactService : IContactService
{
    private readonly PayrollDbContext _context;
    private readonly ILogger<ContactService> _logger;
    private readonly IEmailSender? _emailSender;

    public ContactService(
        PayrollDbContext context,
        ILogger<ContactService> logger,
        IEmailSender? emailSender = null)
    {
        _context = context;
        _logger = logger;
        _emailSender = emailSender;
    }

    public async Task<bool> SubmitContactMessageAsync(ContactMessage message)
    {
        if (message == null) return false;

        try
        {
            message.SubmittedAtUtc = DateTime.UtcNow;
            message.Status = "New";

            _context.ContactMessages.Add(message);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Contact message #{MessageId} received from {Email} - Subject: {Subject}",
                message.Id, message.WorkEmail, message.Subject);

            // Optional email notification if email sender is active
            if (_emailSender != null)
            {
                try
                {
                    string body = $@"
                        <h3>New Contact Message Received</h3>
                        <p><strong>From:</strong> {message.FullName} ({message.WorkEmail})</p>
                        <p><strong>Subject:</strong> {message.Subject}</p>
                        <p><strong>Message:</strong></p>
                        <p>{message.Message}</p>
                        <p><small>Submitted at {message.SubmittedAtUtc:yyyy-MM-dd HH:mm:ss} UTC</small></p>";

                    await _emailSender.SendEmailAsync("support@seps-payroll.internal", $"[SEPS Contact] {message.Subject}", body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Optional contact notification email could not be delivered.");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist contact message from {Email}", message.WorkEmail);
            return false;
        }
    }
}
