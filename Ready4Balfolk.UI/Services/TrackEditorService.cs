using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Views.Dialogs.EditTrack;

namespace Ready4Balfolk.UI.Services;

/// <summary>Opens the edit dialog for a library track and applies what was decided in it.</summary>
public sealed class TrackEditorService(
    IDanceListStore danceListStore,
    ILibraryIndex libraryIndex,
    ITrackStore trackStore)
{
    private Window? _owner;

    public void SetOwner(Window owner) => _owner = owner;

    public async Task EditAsync(Track track)
    {
        if (_owner is null)
        {
            return;
        }

        var vm = new EditTrackDialogViewModel(track, danceListStore.Index);
        var dialog = new EditTrackDialogView { DataContext = vm };
        await dialog.ShowDialog(_owner);

        if (vm.DialogResult == true && vm.DanceToSave is { } dance)
        {
            await ApplyAsync(track, dance, vm.Artist.Trim(), vm.Title.Trim());
        }
    }

    /// <summary>Approves the changed fields individually and republishes the library.</summary>
    /// <remarks>
    /// Only what changed: an untouched field keeps whatever approval it already had, so one a rule
    /// answered is still taken back when that rule changes. The track never leaves the library; the
    /// rebuild is what makes the correction show at once.
    /// </remarks>
    public async Task ApplyAsync(Track track, string dance, string artist, string title)
    {
        var answers = new List<FieldAnswer>();
        if (!string.Equals(dance, track.Dance, System.StringComparison.Ordinal))
        {
            answers.Add(new FieldAnswer(TrackField.Dance, dance));
        }

        if (!string.Equals(artist, track.Artist, System.StringComparison.Ordinal))
        {
            answers.Add(new FieldAnswer(TrackField.Artist, artist));
        }

        if (!string.Equals(title, track.Title, System.StringComparison.Ordinal))
        {
            answers.Add(new FieldAnswer(TrackField.Title, title));
        }

        if (answers.Count > 0)
        {
            await libraryIndex.ApproveIndividuallyAsync([track.FileInfo.FullName], answers);
            await trackStore.RefreshLibraryAsync();
        }
    }
}
