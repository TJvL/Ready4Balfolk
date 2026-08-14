using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DancePoolTests : IDisposable
{
    private readonly DancePool _sut = new();

    [Fact]
    public void StartsEmpty_WhichMeansEverything()
    {
        Assert.Empty(_sut.Tags);
        Assert.Empty(_sut.ExcludedTags);

        var pool = Assert.IsType<RandomSelectionScope.Pool>(_sut.Scope);
        Assert.Empty(pool.Tags);
        Assert.Empty(pool.ExcludedTags);
    }

    [Fact]
    public void Toggle_WalksATagThroughItsThreeStates()
    {
        _sut.Toggle("bretagne");
        Assert.Equal(["bretagne"], _sut.Tags);
        Assert.Empty(_sut.ExcludedTags);

        _sut.Toggle("bretagne");
        Assert.Empty(_sut.Tags);
        Assert.Equal(["bretagne"], _sut.ExcludedTags);

        _sut.Toggle("bretagne");
        Assert.Empty(_sut.Tags);
        Assert.Empty(_sut.ExcludedTags);
    }

    [Fact]
    public void Toggle_KeepsTheOnesAlreadyIn()
    {
        _sut.Toggle("bretagne");
        _sut.Toggle("waltz");

        Assert.Equal(["bretagne", "waltz"], _sut.Tags);
    }

    [Fact]
    public void IncludedAndExcluded_LiveSideBySide()
    {
        // "bretagne, but never chain": the whole point of the third state.
        _sut.Toggle("bretagne");
        _sut.Toggle("chain");
        _sut.Toggle("chain");

        Assert.Equal(["bretagne"], _sut.Tags);
        Assert.Equal(["chain"], _sut.ExcludedTags);

        var pool = Assert.IsType<RandomSelectionScope.Pool>(_sut.Scope);
        Assert.Equal(["bretagne"], pool.Tags);
        Assert.Equal(["chain"], pool.ExcludedTags);
    }

    [Fact]
    public void Clear_EmptiesBothSides()
    {
        _sut.Toggle("bretagne");
        _sut.Toggle("chain");
        _sut.Toggle("chain");

        _sut.Clear();

        Assert.Empty(_sut.Tags);
        Assert.Empty(_sut.ExcludedTags);
    }

    [Fact]
    public void Observe_ReportsEveryChange()
    {
        var seen = new List<int>();
        using var subscription = _sut.Observe().Subscribe(selection => seen.Add(selection.Tags.Count));

        _sut.Toggle("bretagne");
        _sut.Toggle("waltz");
        _sut.Clear();

        // The panel, the auto-queue and the phone remote all watch this, so it has to say so every
        // time rather than only when asked.
        Assert.Equal([0, 1, 2, 0], seen);
    }

    [Fact]
    public void Clear_WhenAlreadyEmpty_SaysNothing()
    {
        var seen = 0;
        using var subscription = _sut.Observe().Subscribe(_ => seen++);

        _sut.Clear();

        Assert.Equal(1, seen);
    }

    public void Dispose() => _sut.Dispose();
}
