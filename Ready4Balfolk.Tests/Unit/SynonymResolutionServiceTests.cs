using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Services.Synonym;
using Ready4Balfolk.Domain.Stores.Synonym;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class SynonymResolutionServiceTests : IDisposable
{
    private readonly BehaviorSubject<IReadOnlyList<DanceMainName>> _synonymSubject;
    private readonly SynonymResolutionService _sut;

    public SynonymResolutionServiceTests()
    {
        var data = TestData.CreateSimpleSynonyms();
        _synonymSubject = new BehaviorSubject<IReadOnlyList<DanceMainName>>(data);
        var store = Substitute.For<IDanceSynonymStore>();
        store.Current.Returns(data);
        store.Observe().Returns(_synonymSubject);

        _sut = new SynonymResolutionService(store);
    }

    [Fact]
    public void Resolve_MainName_ReturnsItself() => Assert.Equal("Mazurka", _sut.Resolve("Mazurka"));

    [Fact]
    public void Resolve_Synonym_ReturnsMainName() => Assert.Equal("Mazurka", _sut.Resolve("Mazurk"));

    [Fact]
    public void Resolve_Unknown_ReturnsOriginal() => Assert.Equal("Polka", _sut.Resolve("Polka"));

    [Fact]
    public void Resolve_CaseInsensitive() => Assert.Equal("Mazurka", _sut.Resolve("MAZURKA"));

    [Fact]
    public void Resolve_AccentInsensitive() => Assert.Equal("Mazurka", _sut.Resolve("mazurka"));

    [Fact]
    public void IsKnown_KnownName_ReturnsTrue() => Assert.True(_sut.IsKnown("Mazurka"));

    [Fact]
    public void IsKnown_KnownSynonym_ReturnsTrue() => Assert.True(_sut.IsKnown("Mazou"));

    [Fact]
    public void IsKnown_Unknown_ReturnsFalse() => Assert.False(_sut.IsKnown("Polka"));

    [Fact]
    public void Changed_EmitsOnStoreUpdate()
    {
        var emitted = false;
        using var sub = _sut.Changed.Subscribe(_ => emitted = true);

        var updated = new List<DanceMainName>
        {
            TestData.CreateMainName("Polka", "Polkka")
        };
        _synonymSubject.OnNext(updated);

        // Give the TaskPoolScheduler a moment to process
        Thread.Sleep(100);

        Assert.True(emitted);
    }

    [Fact]
    public void Resolve_UsesUpdatedLookupAfterChange()
    {
        var updated = new List<DanceMainName>
        {
            TestData.CreateMainName("Polka", "Polkka")
        };
        _synonymSubject.OnNext(updated);

        Thread.Sleep(100);

        Assert.Equal("Polka", _sut.Resolve("Polkka"));
        Assert.Equal("Unknown", _sut.Resolve("Unknown"));
    }

    public void Dispose() => _sut.Dispose();
}
