using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Views.Dialogs.EditTrack;

namespace Ready4Balfolk.UI.Services;

public sealed class TrackEditorService(
    IDanceListStore danceListStore,
    ILibraryIndex libraryIndex,
    ITrackStore trackStore) : ITrackEditorService
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
