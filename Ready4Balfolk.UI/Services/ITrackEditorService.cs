using System.Threading.Tasks;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.UI.Services;

/// <summary>What can be done about a library track's answer: changed here, or taken back.</summary>
public interface ITrackEditorService
{
    Task EditAsync(Track track);

    /// <summary>Approves the changed fields individually and republishes the library.</summary>
    /// <remarks>
    /// Only what changed: an untouched field keeps whatever approval it already had, so one a rule
    /// answered is still taken back when that rule changes. The track never leaves the library; the
    /// rebuild is what makes the correction show at once. The dance comes in as the name the person
    /// read, which is what "changed" is decided on, and goes down as the slug it stands for.
    /// </remarks>
    Task ApplyAsync(Track track, string dance, string artist, string title);

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
    Task<bool> WithdrawAsync(Track track);
}
