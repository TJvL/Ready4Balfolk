using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DancePoolTests
{
    private readonly DancePool _sut = new();

    [Fact]
    public void StartsEmpty_WhichMeansEverything()
    {
        Assert.Empty(_sut.Tags);

        var pool = Assert.IsType<RandomSelectionScope.Pool>(_sut.Scope);
        Assert.Empty(pool.Tags);
    }

    [Fact]
    public void Toggle_AddsThenRemoves()
    {
        _sut.Toggle("bretagne");
        Assert.Equal(["bretagne"], _sut.Tags);

        _sut.Toggle("bretagne");
        Assert.Empty(_sut.Tags);
    }

    [Fact]
    public void Toggle_KeepsTheOnesAlreadyIn()
    {
        _sut.Toggle("bretagne");
        _sut.Toggle("waltz");

        Assert.Equal(["bretagne", "waltz"], _sut.Tags);
    }

    [Fact]
    public void Clear_EmptiesIt()
    {
        _sut.Toggle("bretagne");

        _sut.Clear();

        Assert.Empty(_sut.Tags);
    }

    [Fact]
    public void Observe_ReportsEveryChange()
    {
        var seen = new List<int>();
        using var subscription = _sut.Observe().Subscribe(tags => seen.Add(tags.Count));

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
}
