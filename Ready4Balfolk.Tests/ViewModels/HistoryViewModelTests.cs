using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.History;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class HistoryViewModelTests : IDisposable
{
    private readonly IQueueHistoryStore _historyStore;
    private readonly IConfirmationService _confirmation;
    private readonly BehaviorSubject<QueueHistory> _historySubject;
    private readonly HistoryViewModel _sut;

    public HistoryViewModelTests()
    {
        _historySubject = new BehaviorSubject<QueueHistory>(new QueueHistory(null, []));
        _historyStore = Substitute.For<IQueueHistoryStore>();
        _historyStore.Observe().Returns(_historySubject);

        _confirmation = Substitute.For<IConfirmationService>();
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        _sut = new HistoryViewModel(_historyStore, _confirmation);
    }

    [Fact]
    public void InitialState_NoHistory()
    {
        Assert.Equal("No history", _sut.ItemCountText);
        Assert.Equal("", _sut.TotalDurationText);
        Assert.False(_sut.HasItems);
    }

    [Fact]
    public void HistoryChange_PopulatesItems()
    {
        var history = new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/a.mp3", "Mazurka", "A", "T",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Finished)
        ]);

        _historySubject.OnNext(history);

        Assert.Single(_sut.Items);
        Assert.True(_sut.HasItems);
    }

    [Fact]
    public void ItemCountText_None() => Assert.Equal("No history", _sut.ItemCountText);

    [Fact]
    public void ItemCountText_Singular()
    {
        _historySubject.OnNext(new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/a.mp3", "M", "A", "T",
                TimeSpan.FromMinutes(1), false, CompletionStatus.Finished)
        ]));

        Assert.Equal("1 item", _sut.ItemCountText);
    }

    [Fact]
    public void ItemCountText_Plural()
    {
        _historySubject.OnNext(new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/a.mp3", "M", "A", "T",
                TimeSpan.FromMinutes(1), false, CompletionStatus.Finished),
            new StopHistoryEntry(CompletionStatus.Finished)
        ]));

        Assert.Equal("2 items", _sut.ItemCountText);
    }

    [Fact]
    public void TotalDurationText_FormatsCorrectly()
    {
        _historySubject.OnNext(new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/a.mp3", "M", "A", "T",
                TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(15), false, CompletionStatus.Finished)
        ]));

        Assert.Equal("3:15", _sut.TotalDurationText);
    }

    [Fact]
    public void StartNewNight_WithConfirmation_EndsTheNight()
    {
        _historySubject.OnNext(new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/a.mp3", "M", "A", "T",
                TimeSpan.FromMinutes(1), false, CompletionStatus.Finished)
        ]));

        Assert.True(_sut.HasItems);

        _sut.StartNewNightCommand.Execute().Subscribe();

        _historyStore.Received(1).EndNightAsync();
    }

    [Fact]
    public void StartNewNight_WithoutConfirmation_KeepsTheNightRunning()
    {
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        _historySubject.OnNext(new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/a.mp3", "M", "A", "T",
                TimeSpan.FromMinutes(1), false, CompletionStatus.Finished)
        ]));

        _sut.StartNewNightCommand.Execute().Subscribe();

        _historyStore.DidNotReceive().EndNightAsync();
    }

    [Fact]
    public void DeleteNight_WithConfirmation_Deletes()
    {
        _historySubject.OnNext(new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/a.mp3", "M", "A", "T",
                TimeSpan.FromMinutes(1), false, CompletionStatus.Finished)
        ]));

        _sut.DeleteNightCommand.Execute().Subscribe();

        _historyStore.Received(1).DeleteNightAsync();
    }

    [Fact]
    public void DeleteNight_WithoutConfirmation_KeepsIt()
    {
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        _historySubject.OnNext(new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/a.mp3", "M", "A", "T",
                TimeSpan.FromMinutes(1), false, CompletionStatus.Finished)
        ]));

        _sut.DeleteNightCommand.Execute().Subscribe();

        _historyStore.DidNotReceive().DeleteNightAsync();
    }

    public void Dispose()
    {
        _sut.Dispose();
        _historySubject.Dispose();
    }
}
