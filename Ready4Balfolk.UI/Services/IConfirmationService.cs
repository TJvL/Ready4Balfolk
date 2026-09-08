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
    /// <para>
    /// <paramref name="stakes" /> says which answer the return key, the initial focus and the accent
    /// belong to. It defaults to <see cref="ConfirmationStakes.Destructive" />, because a question
    /// nobody thought about is one the DJ should have to mean.
    /// </para>
    /// <para>
    /// <paramref name="confirmText" /> and <paramref name="cancelText" /> default to null, which
    /// means "Yes" and "No" in the current language: a resource lookup cannot be a C# default
    /// parameter value, so the implementation resolves it.
    /// </para>
    /// </remarks>
    Task<bool> ConfirmAsync(string title, string message,
        string? confirmText = null, string? cancelText = null,
        ConfirmationStakes stakes = ConfirmationStakes.Destructive,
        CancellationToken cancellationToken = default);
}
