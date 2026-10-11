using Microsoft.Extensions.FileProviders;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The vendored SignalR client against the SignalR the server runs on. The smoke test asks the
/// same question of a running package; this asks it of the source, on every pull request, so a
/// runtime bump that leaves the browser client a major behind is red before it is packaged.
/// </summary>
public sealed class SignalRVersionsTests
{
    private static readonly Version Server = new(10, 0, 11);

    [Fact]
    public void TheVendoredClient_IsOnTheSameMajorAsTheServer()
    {
        var mismatch = SignalRVersions.Mismatch(VendoredClient());

        Assert.Null(mismatch);
    }

    [Fact]
    public void TheServersVersion_IsReadFromTheRuntime()
    {
        // The assembly version alone would stop at 10.0.0.0; the smoke test prints this, and a
        // person reading "10.0.0" against a client at 10.0.11 would go looking for a drift that
        // is not there.
        Assert.NotEqual(new Version(0, 0), SignalRVersions.Server);
        Assert.True(SignalRVersions.Server.Major >= 10, $"server reads as {SignalRVersions.Server}");
    }

    [Fact]
    public void TheBanner_GivesTheClientsVersion()
    {
        var script = "/*! @microsoft/signalr 10.0.11, the ASP.NET Core SignalR JavaScript client.\n"
                     + " * Copyright (c) .NET Foundation and Contributors. */\nvar t,e;";

        Assert.Equal(new Version(10, 0, 11), SignalRVersions.ReadClient(script));
    }

    [Fact]
    public void AVersionBelowTheFirstLine_IsNotTheBanner()
    {
        // Minified code mentions versions of its own, such as the protocol's, and none of them is
        // the package version.
        var script = "var t,e;\n/*! @microsoft/signalr 10.0.11 */";

        Assert.Null(SignalRVersions.ReadClient(script));
        Assert.Contains("no version banner", SignalRVersions.Mismatch(script, Server), StringComparison.Ordinal);
    }

    [Fact]
    public void AClientAMajorApart_IsAMismatchThatNamesBothVersions()
    {
        var mismatch = SignalRVersions.Mismatch("/*! @microsoft/signalr 9.0.6, the client. */\nvar t;", Server);

        Assert.NotNull(mismatch);
        Assert.Contains("9.0.6", mismatch, StringComparison.Ordinal);
        Assert.Contains("10.0.11", mismatch, StringComparison.Ordinal);
    }

    [Fact]
    public void AClientAPatchOrMinorApart_IsNotAMismatch()
    {
        // The two are compatible across patch and minor versions, and the runtime moves monthly
        // while the vendored file does not: a check that failed on a patch would fail every month.
        Assert.Null(SignalRVersions.Mismatch("/*! @microsoft/signalr 10.0.4, the client. */", Server));
        Assert.Null(SignalRVersions.Mismatch("/*! @microsoft/signalr 10.2.0, the client. */", Server));
    }

    /// <summary>The file as the server serves it, out of the Web assembly rather than from disk.</summary>
    private static string VendoredClient()
    {
        var assets = new ManifestEmbeddedFileProvider(typeof(PresentationWebServer).Assembly, "wwwroot");
        using var stream = assets.GetFileInfo(SignalRVersions.ClientAsset).CreateReadStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
