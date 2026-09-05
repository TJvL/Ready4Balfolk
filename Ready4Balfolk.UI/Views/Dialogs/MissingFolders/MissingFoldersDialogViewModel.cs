using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Dialogs.MissingFolders;

/// <summary>The question a scan asks when it cannot tell an empty folder from an absent one.</summary>
/// <remarks>
/// Names each folder and what the index holds in it, and says nothing about why. A scan does not
/// know whether a drive failed to mount or somebody cleared out a folder on purpose, and inventing
/// a reason is what makes a person agree to the wrong thing.
/// </remarks>
public sealed class MissingFoldersDialogViewModel : ReactiveObject
{
    public MissingFoldersDialogViewModel(IReadOnlyList<MissingLibraryFolder> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        Folders = [.. folders.Select(Describe)];
        Consequence = string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.MissingFolders_Consequence,
            folders.Sum(folder => folder.TrackCount));

        KeepThemCommand = ReactiveCommand.Create(() => Answer = MissingFolderAnswer.KeepThem);
        ForgetThemCommand = ReactiveCommand.Create(() => Answer = MissingFolderAnswer.ForgetThem);
        ExitCommand = ReactiveCommand.Create(() => Answer = MissingFolderAnswer.Exit);
    }

    /// <summary>One line per folder, each already in the words the user reads.</summary>
    public IReadOnlyList<string> Folders { get; }

    /// <summary>What each of the three answers does, with the number it does it to.</summary>
    public string Consequence { get; }

    /// <summary>Null until one of the three is pressed, which is what closes the window.</summary>
    public MissingFolderAnswer? Answer
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ICommand KeepThemCommand { get; }

    public ICommand ForgetThemCommand { get; }

    public ICommand ExitCommand { get; }

    private static string Describe(MissingLibraryFolder folder) => folder.Error is { Length: > 0 } error
        ? string.Format(
            CultureInfo.CurrentCulture, UiStrings.MissingFolders_Unreadable,
            folder.Path, error, folder.TrackCount)
        : string.Format(
            CultureInfo.CurrentCulture, UiStrings.MissingFolders_Empty, folder.Path, folder.TrackCount);
}
