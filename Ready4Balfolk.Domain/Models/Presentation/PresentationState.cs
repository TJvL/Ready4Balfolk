namespace Ready4Balfolk.Domain.Models.Presentation;

/// <summary>What every presentation surface shows, independent of how it draws it.</summary>
/// <param name="Current">The item playing now.</param>
/// <param name="Next">The item after it.</param>
/// <param name="Behind">
/// The dance waiting behind a pause, or nothing.
/// </param>
/// <param name="IsPlaying">Whether playback is running rather than paused.</param>
/// <remarks>
/// A delay, a stop or a message is often queued precisely so the room can get ready for what
/// follows: form groups, make lines, find a partner. During that pause the one thing the dancers
/// want to know is which dance is actually coming, and it is exactly then that a screen showing
/// only the pause stops answering.
/// </remarks>
public sealed record PresentationState(
    PresentationItem Current,
    PresentationItem Next,
    PresentationItem Behind,
    bool IsPlaying)
{
    /// <summary>An idle floor: nothing playing and nothing queued.</summary>
    public static readonly PresentationState Empty =
        new(PresentationItem.None, PresentationItem.None, PresentationItem.None, false);

    /// <summary>Whether anything is playing.</summary>
    public bool HasCurrent => Current.HasContent;

    /// <summary>Whether anything follows.</summary>
    public bool HasNext => Next.HasContent;

    /// <summary>Whether a dance is waiting behind what is next.</summary>
    public bool HasBehind => Behind.HasContent;
}
