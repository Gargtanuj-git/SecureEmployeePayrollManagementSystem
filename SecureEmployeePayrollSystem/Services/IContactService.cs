using System.Threading.Tasks;
using SecureEmployeePayrollSystem.Models;

namespace SecureEmployeePayrollSystem.Services;

public interface IContactService
{
    Task<bool> SubmitContactMessageAsync(ContactMessage message);
}
