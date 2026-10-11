using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace Ready4Balfolk.Web;

/// <summary>
/// The two halves of SignalR this application ships, and whether they still speak to each other.
/// </summary>
/// <remarks>
/// <para>
/// The browser client is vendored at <c>wwwroot/lib/signalr.min.js</c>, because the display page
/// and the phone remote have to load in a hall with no internet. The server half is the SignalR
/// inside the ASP.NET Core shared framework, which every runtime bump raises. Dependabot watches
/// NuGet and Actions and sees neither file, so nothing said when the two drifted apart.
/// </para>
/// <para>
/// The client and server are compatible across minor and patch versions, so only the major is held.
/// A major apart fails at the handshake, which in practice means on the first night after a
/// release, in front of a room, with no way to tell from the build that it would.
/// </para>
/// </remarks>
public static partial class SignalRVersions
{
    /// <summary>Where the vendored client is served from, relative to the server's root.</summary>
    public const string ClientAsset = "lib/signalr.min.js";

    /// <summary>
    /// The SignalR the server runs on, read from the assembly the hubs derive from, so it is the
    /// runtime that actually loaded rather than the one the build was made against.
    /// </summary>
    public static Version Server => ReadServer(typeof(Hub).Assembly);

    /// <summary>
    /// Why the vendored client cannot be trusted against the running server, or <c>null</c> when
    /// it can. Phrased for the smoke test's report, with both versions in it, so the fix is read
    /// off the failure: replace the vendored file in the pull request that raised the runtime.
    /// </summary>
    public static string? Mismatch(string clientScript) => Mismatch(clientScript, Server);

    /// <inheritdoc cref="Mismatch(string)"/>
    public static string? Mismatch(string clientScript, Version server)
    {
        ArgumentNullException.ThrowIfNull(clientScript);
        ArgumentNullException.ThrowIfNull(server);

        return ReadClient(clientScript) switch
        {
            null => $"{ClientAsset} has no version banner on its first line, so it cannot be held "
                    + $"against the server's SignalR {server}",
            var client when client.Major != server.Major =>
                $"{ClientAsset} is SignalR client {client} and the server runs SignalR {server}; "
                + "a major version apart fails at the handshake, so replace the vendored file "
                + "alongside the runtime",
            _ => null
        };
    }

    /// <summary>
    /// The version in the banner the vendored file opens with, or <c>null</c> when the first line
    /// is not that banner. Only the first line is read: the rest is minified code, and a version
    /// string found somewhere inside it would be anybody's.
    /// </summary>
    public static Version? ReadClient(string clientScript)
    {
        ArgumentNullException.ThrowIfNull(clientScript);

        var firstLine = clientScript.AsSpan();
        var lineEnd = firstLine.IndexOfAny('\r', '\n');
        if (lineEnd >= 0)
        {
            firstLine = firstLine[..lineEnd];
        }

        var match = ClientBanner().Match(firstLine.ToString());
        return match.Success && Version.TryParse(match.Groups["version"].Value, out var version)
            ? version
            : null;
    }

    /// <summary>
    /// The informational version is the one a person would quote (10.0.11); the assembly version
    /// stops at the major (10.0.0.0). The build metadata after the plus sign is a commit hash, and
    /// not a version at all.
    /// </summary>
    private static Version ReadServer(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informational is not null)
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            if (Version.TryParse(plus >= 0 ? informational[..plus] : informational, out var version))
            {
                return version;
            }
        }

        return assembly.GetName().Version ?? new Version(0, 0);
    }

    /// <summary>The npm package name and its version, as the file's own banner states them.</summary>
    [GeneratedRegex(@"@microsoft/signalr (?<version>\d+\.\d+\.\d+)")]
    private static partial Regex ClientBanner();
}
