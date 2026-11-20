using System.Collections.Generic;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.ViewModels;

public class HomePublicViewModel
{
    public List<UserFeedback> FeaturedFeedback { get; set; } = new();
    public ContactMessage ContactForm { get; set; } = new();
    public UserFeedback FeedbackForm { get; set; } = new();
    public string? StatusMessage { get; set; }
    public bool IsSuccess { get; set; }
}
