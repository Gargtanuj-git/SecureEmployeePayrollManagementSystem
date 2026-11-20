using System.Collections.Generic;
using System.Threading.Tasks;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public interface IFeedbackService
{
    Task<bool> SubmitFeedbackAsync(UserFeedback feedback);
    Task<List<UserFeedback>> GetFeaturedFeedbackAsync();
}
