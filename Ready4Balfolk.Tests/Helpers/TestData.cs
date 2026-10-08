using System.IO.Abstractions.TestingHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;

namespace Ready4Balfolk.Tests.Helpers;

public static class TestData
{
    // One shared in-memory filesystem for every fixture-built track: the tracks only ever need a
    // path identity, never a file that exists.
    private static readonly MockFileSystem FileSystem = new();

    /// <summary>
    /// A track. <paramref name="slug"/> defaults to the dance name lowercased, which matches the
    /// slugs in <see cref="CreateSimpleDanceList"/>; pass null for a track the list does not know.
    /// </summary>
    public static Track CreateTrack(string dance = "Mazurka", string artist = "Artist",
        string title = "Title", int lengthSeconds = 180, AudioFormat format = AudioFormat.Mp3,
        string? slug = "")
        => new(dance, artist, title,
            FileSystem.FileInfo.New($"/tmp/test/{dance}_{artist}_{title}.mp3".Replace(' ', '_')),
            TimeSpan.FromSeconds(lengthSeconds), format)
        {
            DanceSlug = slug == string.Empty ? dance.ToLowerInvariant() : slug
        };

    public static Dance CreateDance(string slug, string[]? tags = null, params string[] names)
        => new()
        {
            Slug = slug,
            Names = names.Length > 0 ? names : [slug],
            Tags = tags ?? []
        };

    /// <summary>
    /// Standard dance list, in the shape BigBalfolkList publishes:
    /// mazurka [Mazurka, Mazurk]      common
    /// scottish [Scottish, Schottische] common
    /// plinn [Plinn]                  bretagne, suite
    /// </summary>
    public static DanceList CreateSimpleDanceList()
        => new()
        {
            Tags = ["bretagne", "common", "suite"],
            Dances =
            [
                CreateDance("mazurka", ["common"], "Mazurka", "Mazurk"),
                CreateDance("scottish", ["common"], "Scottish", "Schottische"),
                CreateDance("plinn", ["bretagne", "suite"], "Plinn")
            ]
        };

    /// <summary>
    /// What discovery read off one file: <paramref name="fileName"/> gets an mp3 extension, and the
    /// file sits in a folder called Artist unless <paramref name="segments"/> says otherwise.
    /// </summary>
    public static TrackEvidence CreateEvidence(string fileName, IReadOnlyList<string>? segments = null) => new()
    {
        FileName = fileName + ".mp3",
        PathSegments = segments ?? ["Artist"],
        Duration = TimeSpan.FromSeconds(180),
        Format = AudioFormat.Mp3,
        ContentHash = [1]
    };

    /// <summary>A mazurka that played to the end, as the night's history remembers it.</summary>
    public static TrackHistoryEntry CreateHistoryEntry(string artist, string title, DateTime startedAt) => new(
        Path.Combine(Path.GetTempPath(), "mazurka.mp3"), "Mazurka", artist, title,
        TimeSpan.FromMinutes(3), false, CompletionStatus.Finished, startedAt);

    /// <summary>
    /// A connection under a hub, opened with <paramref name="token"/> on the query string, which
    /// is where SignalR keeps it. Pass null for a phone that brought no token at all.
    /// </summary>
    public static HubCallerContext CreateHubConnection(string connectionId, string? token)
    {
        var http = new DefaultHttpContext();
        if (token is not null)
        {
            http.Request.QueryString = QueryString.Create("access_token", token);
        }

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new CarriedHttpContext(http));

        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);
        context.Features.Returns(features);
        return context;
    }

    /// <summary>How SignalR hands the opening request through to a live connection.</summary>
    private sealed class CarriedHttpContext(HttpContext context) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = context;
    }
}
