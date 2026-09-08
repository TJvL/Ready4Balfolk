namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>Something the panel keeps across a rebuild rather than draws again.</summary>
/// <remarks>
/// <para>
/// The panel is rebuilt whenever the list, the pool, the search or the library moves, two of them
/// on throttles of their own, so a rebuild lands while somebody is using it. A card or a chip that
/// was replaced took its control with it, and Avalonia moves the keyboard nowhere when the control
/// standing under it is detached: the DJ tabs to a dice and a fraction of a second later the space
/// they press runs the transport instead. So the panel matches what it is showing to what it has
/// by key: only what is genuinely new is built, and what is still wanted is told what it says now
/// and keeps the control it is drawn as.
/// </para>
/// <para>
/// Staying put is the other half of it, and that half no collection can give. An item shown at a
/// different index is built a fresh container by Avalonia wherever it lands, so a card the panel
/// has to move loses its control however carefully it was kept. The key is what makes that
/// survivable too: the view finds the same item's control again and puts the keyboard back on it.
/// </para>
/// </remarks>
internal interface IKeepsItsPlace
{
    /// <summary>What makes this the same one across a rebuild: a dance's slug, or a tag.</summary>
    string Key { get; }
}
