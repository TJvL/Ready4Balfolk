using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.UI.Views.Dialogs.MissingFolders;

namespace Ready4Balfolk.UI.Services;

/// <summary>Puts a scan's question about a folder with no music in it in front of the user.</summary>
/// <remarks>
/// <para>
/// The scan runs off the UI thread and knows nothing about windows, so this is where the two meet:
/// it marshals onto the dispatcher, shows the dialog, and hands the answer back. The owner comes
/// from <see cref="ConfirmationService"/> rather than being set a second time, so a question raised
/// while the setup wizard is up is parented to the wizard, exactly as a confirmation is.
/// </para>
/// <para>
/// Keeping the tracks is what an unanswered question means: the smoke test, a window that is not up
/// yet, and a dialog closed from the title bar all land there. It is the one answer that cannot
/// lose anything, and a scan that runs with nobody watching must never be the thing that deletes a
/// library.
/// </para>
/// </remarks>
public sealed class MissingFolderPromptService(ConfirmationService owner, ILoggerService loggerService)
    : IMissingFolderPrompt
{
    private Func<Task>? _exit;

    /// <summary>What "exit" does, supplied by startup, which owns the window and the shutdown.</summary>
    public void SetExit(Func<Task> exit) => _exit = exit;

    public async Task<MissingFolderAnswer> AskAsync(
        IReadOnlyList<MissingLibraryFolder> folders, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (Program.IsSmokeTest || owner.CurrentOwner is not { } window)
        {
            return MissingFolderAnswer.KeepThem;
        }

        var answer = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var viewModel = new MissingFoldersDialogViewModel(folders);
            var dialog = new MissingFoldersDialogView { DataContext = viewModel };
            await dialog.ShowDialog(window);

            return viewModel.Answer ?? MissingFolderAnswer.KeepThem;
        });

        if (answer is MissingFolderAnswer.Exit && _exit is { } exit)
        {
            // Posted rather than awaited: the scan is waiting on this answer, and closing the
            // application out from under it would leave that continuation on a dispatcher that is
            // going away. It returns first, writes nothing, and the shutdown follows.
            Dispatcher.UIThread.Post(() => exit().SafeFireAndForget(exception =>
                loggerService.ErrorAsync("Failed to close after the library question", exception)));
        }

        return answer;
    }
}
