using System.Threading.Tasks;

namespace Ready4Balfolk.UI.Services;

public interface IConfirmationService
{
    Task<bool> ConfirmAsync(string title, string message,
        string confirmText = "Yes", string cancelText = "No");
}
