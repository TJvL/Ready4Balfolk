using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Views.Dialogs.EditTrack;

namespace Ready4Balfolk.UI.Services;

/// <summary>What can be done about a library track's answer: changed here, or taken back.</summary>
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
    /// rebuild is what makes the correction show at once. The dance comes in as the name the person
    /// read, which is what "changed" is decided on, and goes down as the slug it stands for.
    /// </remarks>
    public async Task ApplyAsync(Track track, string dance, string artist, string title)
    {
        var answers = new List<FieldAnswer>();
        if (!string.Equals(dance, track.Dance, System.StringComparison.Ordinal))
        {
            answers.Add(new FieldAnswer(TrackField.Dance, danceListStore.Index.ApprovedValueFor(dance)));
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

    /// <summary>
    /// Takes back the answer somebody gave this track, so it leaves the library and waits again.
    /// </summary>
    /// <remarks>
    /// The lasting way out of an individual approval, and the reason it lives beside the library
    /// rather than only in the review queue: that queue is rebuilt from the index on every scan and
    /// drops everything already in the library, so a track answered last week has no row to press a
    /// button on. Where it does still exist is here, in the list of what got through the gate.
    /// </remarks>
    /// <returns>
    /// False when nothing was taken back, which is a track in the library on its rules or its tags
    /// rather than on anything a person answered. Saying so is the caller's job: silently doing
    /// nothing reads as the command having failed.
    /// </returns>
    public async Task<bool> WithdrawAsync(Track track)
    {
        var taken = await libraryIndex.WithdrawIndividualApprovalsAsync([track.FileInfo.FullName]);
        if (taken == 0)
        {
            return false;
        }

        // The track is out of the library the moment the answer is, because that is the whole
        // point: it is a question again and nothing may draw it. What is already in tonight's queue
        // stays there and still plays.
        await trackStore.RefreshLibraryAsync();
        return true;
    }
}
