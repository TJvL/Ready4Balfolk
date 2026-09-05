using System.Threading;
using System.Threading.Tasks;

namespace Ready4Balfolk.UI.Services;

public interface IConfirmationService
{
    /// <summary>Asks the person a yes or no question.</summary>
    /// <remarks>
    /// Cancelling the token withdraws the question: the dialog closes on its own and the answer is
    /// no. For one that has stopped being about anything, such as a question about a dance that has
    /// already ended.
    /// </remarks>
    Task<bool> ConfirmAsync(string title, string message,
        string confirmText = "Yes", string cancelText = "No",
        CancellationToken cancellationToken = default);
}
