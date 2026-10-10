using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Views.Dialogs.EditTrack;

namespace Ready4Balfolk.UI.Services;

public sealed class TrackEditorService(
    IDanceListStore danceListStore,
    ILibraryIndex libraryIndex,
    ITrackStore trackStore,
    ISettingsStore settingsStore,
    DialogOwner owner) : ITrackEditorService
{
    public async Task EditAsync(Track track)
    {
        if (owner.Current is not { } window)
        {
            return;
        }

        // The library's own copy where it has one. A track opened from the queue is the snapshot
        // taken when it was queued, and its likelihood and equalizer are whatever they were then:
        // a curve pulled on the main screen while it played is on the library's copy alone.
        var current = trackStore.Current.FirstOrDefault(candidate =>
            string.Equals(candidate.FileInfo.FullName, track.FileInfo.FullName, StringComparison.Ordinal)) ?? track;

        var vm = new EditTrackDialogViewModel(current, danceListStore.Index, settingsStore.Current.Equalizer);
        var dialog = new EditTrackDialogView { DataContext = vm };
        await dialog.ShowDialog(window);

        if (vm.DialogResult == true && vm.DanceToSave is { } dance)
        {
            await ApplyAsync(current, dance, vm.Artist.Trim(), vm.Title.Trim(), vm.Likelihood, vm.EqualizerToSave);
        }
    }

    public async Task ApplyAsync(
        Track track, string dance, string artist, string title, double likelihood, EqualizerSettings? equalizer)
    {
        var answers = new List<FieldAnswer>();
        if (!string.Equals(dance, track.Dance, StringComparison.Ordinal))
        {
            answers.Add(new FieldAnswer(TrackField.Dance, danceListStore.Index.ApprovedValueFor(dance)));
        }

        if (!string.Equals(artist, track.Artist, StringComparison.Ordinal))
        {
            answers.Add(new FieldAnswer(TrackField.Artist, artist));
        }

        if (!string.Equals(title, track.Title, StringComparison.Ordinal))
        {
            answers.Add(new FieldAnswer(TrackField.Title, title));
        }

        var likelihoodMoved = TrackLikelihood.Normalize(likelihood) != track.Likelihood;

        if (answers.Count > 0)
        {
            await libraryIndex.ApproveIndividuallyAsync([track.FileInfo.FullName], answers);
        }

        if (likelihoodMoved)
        {
            await libraryIndex.SetLikelihoodAsync([track.FileInfo.FullName], likelihood);
        }

        var equalizerChanged = !Equals(equalizer, track.Equalizer);
        if (equalizerChanged)
        {
            await libraryIndex.SetEqualizerAsync([track.FileInfo.FullName], equalizer);
        }

        if (answers.Count > 0 || likelihoodMoved || equalizerChanged)
        {
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
