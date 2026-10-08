using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Helpers;

/// <summary>Lets TagLib open a file through the file system it was found on.</summary>
/// <remarks>
/// Handing TagLib a path makes it open the file on the real disk, whatever file system the caller
/// was given, so a test on a mock file system passes every check and then fails the read. Going
/// through the <see cref="IFileInfo"/> keeps the tag read on the same file system as everything
/// else that touches the file.
/// </remarks>
internal sealed class TagLibFileAbstraction(IFileInfo fileInfo) : TagLib.File.IFileAbstraction
{
    // The full path, as TagLib's own local abstraction gives it: TagLib picks the format by the
    // extension of this name, and puts it in the messages it throws.
    public string Name => fileInfo.FullName;

    // Shared for reading, as TagLib opens a local file, so a player holding the same file open
    // does not stop its tags being read.
    public Stream ReadStream => fileInfo.FileSystem.FileStream.New(
        fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

    public Stream WriteStream => fileInfo.FileSystem.FileStream.New(
        fileInfo.FullName, FileMode.Open, FileAccess.ReadWrite);

    public void CloseStream(Stream stream) => stream.Dispose();
}
