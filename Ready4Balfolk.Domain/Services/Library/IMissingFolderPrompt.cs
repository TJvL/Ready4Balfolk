namespace Ready4Balfolk.Domain.Services.Library;

/// <summary>Asks the one person who knows what a folder with no music in it means.</summary>
/// <remarks>
/// <para>
/// A scan cannot tell a drive that has not mounted from a folder emptied on purpose: from inside
/// the filesystem they are the same nothing, and guessing wrong deletes the approvals, which are
/// the one thing a rescan cannot work out again. So the scan stops guessing and asks.
/// </para>
/// <para>
/// Declared here because the scan is what asks, and implemented by the host, which is the same
/// arrangement <c>IRemoteCommandDispatcher</c> is under: the domain has no idea what a dialog is,
/// and the layer that draws one has no business deciding when the library is at risk.
/// </para>
/// </remarks>
public interface IMissingFolderPrompt
{
    /// <summary>Names the folders and waits for an answer. Never called with an empty list.</summary>
    Task<MissingFolderAnswer> AskAsync(
        IReadOnlyList<MissingLibraryFolder> folders, CancellationToken token = default);
}
