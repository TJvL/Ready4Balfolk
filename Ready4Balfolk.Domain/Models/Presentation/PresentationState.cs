namespace Ready4Balfolk.Domain.Models.Presentation;

/// <summary>What every presentation surface shows, independent of how it draws it.</summary>
/// <param name="Current">The item playing now.</param>
/// <param name="Next">The item after it.</param>
/// <param name="IsPlaying">Whether playback is running rather than paused.</param>
public sealed record PresentationState(PresentationItem Current, PresentationItem Next, bool IsPlaying)
{
    /// <summary>An idle floor: nothing playing and nothing queued.</summary>
    public static readonly PresentationState Empty =
        new(PresentationItem.None, PresentationItem.None, false);

    /// <summary>Whether anything is playing.</summary>
    public bool HasCurrent => Current.HasContent;

    /// <summary>Whether anything follows.</summary>
    public bool HasNext => Next.HasContent;
}
