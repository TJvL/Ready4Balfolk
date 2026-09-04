using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;
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
        _historyStore.ListNightsAsync().Returns(Task.FromResult<IReadOnlyList<NightSummary>>([]));
        _historyStore.Current.Returns(_ => _historySubject.Value);

        _confirmation = Substitute.For<IConfirmationService>();
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var settingsStore = Substitute.For<ISettingsStore>();
        var settings = new ApplicationSettings();
        settingsStore.Current.Returns(settings);
        settingsStore.Observe().Returns(new BehaviorSubject<ApplicationSettings>(settings));

        _sut = new HistoryViewModel(
            _historyStore, settingsStore, _confirmation, Substitute.For<ILoggerService>());
    }

    [Fact]
    public void InitialState_NoHistory()
    {
        Assert.Equal("No history", _sut.ItemCountText);
        Assert.Empty(_sut.Items);
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

        // The entry, and the line saying where the night began.
        Assert.Single(_sut.Items, item => !item.IsMarker);
        Assert.Single(_sut.Items, item => item.IsMarker);
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
    public async Task AFiledNight_CanBeChosenAndRead()
    {
        var filed = new QueueHistory(Yesterday, [Track("Salamandre")])
        {
            Id = 7,
            EndedAt = Yesterday.AddHours(4)
        };
        _historyStore.ListNightsAsync().Returns(Task.FromResult<IReadOnlyList<NightSummary>>(
            [new NightSummary(7, Yesterday, Yesterday.AddHours(4), 1)]));
        _historyStore.ReadNightAsync(7).Returns(Task.FromResult<QueueHistory?>(filed));

        await _sut.RefreshNightsAsync();
        _sut.SelectedNight = _sut.Nights.Single(night => !night.IsTonight);

        // The night's own boundaries are lines in it, so an account of an evening says which
        // evening it is and when it was called.
        Assert.Contains(_sut.Items, item => item.Description.Contains("Salamandre", StringComparison.Ordinal));
        Assert.Equal(2, _sut.Items.Count(item => item.IsMarker));
    }

    [Fact]
    public async Task WhenTonightIsEmptyAndNobodyHasChosen_TheLastEveningIsShown()
    {
        // Opening the tab on a machine that holds a season of dancing must not be a blank list
        // under the words "no history".
        _historyStore.ListNightsAsync().Returns(Task.FromResult<IReadOnlyList<NightSummary>>(
            [new NightSummary(7, Yesterday, Yesterday.AddHours(4), 1)]));
        _historyStore.ReadNightAsync(7).Returns(Task.FromResult<QueueHistory?>(
            new QueueHistory(Yesterday, [Track("Salamandre")]) { Id = 7, EndedAt = Yesterday.AddHours(4) }));

        await _sut.RefreshNightsAsync();

        Assert.False(_sut.SelectedNight!.IsTonight);
        Assert.Contains(_sut.Items, item => item.Description.Contains("Salamandre", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_WritesTheNightThatIsBeingRead()
    {
        _historyStore.ListNightsAsync().Returns(Task.FromResult<IReadOnlyList<NightSummary>>(
            [new NightSummary(7, Yesterday, Yesterday.AddHours(4), 1)]));
        _historyStore.ReadNightAsync(7).Returns(Task.FromResult<QueueHistory?>(
            new QueueHistory(Yesterday, [Track("Salamandre")]) { Id = 7, EndedAt = Yesterday.AddHours(4) }));

        await _sut.RefreshNightsAsync();
        await _sut.ExportAsync("/tmp/for the organisers.json");

        // The night on screen, not the empty one that is running.
        await _historyStore.Received(1).ExportAsync(7, "/tmp/for the organisers.json");
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

        _historyStore.Received(1).DeleteNightAsync(Arg.Any<long>());
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

        _historyStore.DidNotReceive().DeleteNightAsync(Arg.Any<long>());
    }

    private static readonly DateTime Yesterday = DateTime.Now.AddDays(-1);

    private static TrackHistoryEntry Track(string title) => new(
        "/tmp/a.mp3", "Mazurka", "Naragonia", title, TimeSpan.FromMinutes(3), false,
        CompletionStatus.Finished, Yesterday, Yesterday.AddMinutes(3));

    public void Dispose()
    {
        _sut.Dispose();
        _historySubject.Dispose();
    }
}
