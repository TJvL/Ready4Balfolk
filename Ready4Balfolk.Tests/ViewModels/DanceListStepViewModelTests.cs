using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Wizard;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class DanceListStepViewModelTests : IDisposable
{
    private readonly BehaviorSubject<DanceList> _listSubject = new(DanceList.Empty);
    private readonly IDanceListStore _store = Substitute.For<IDanceListStore>();
    private readonly IConfirmationService _confirmations = Substitute.For<IConfirmationService>();
    private readonly DanceListStepViewModel _sut;

    public DanceListStepViewModelTests()
    {
        _store.Current.Returns(_ => _listSubject.Value);
        _store.ReplaceAsync(Arg.Any<DanceList>()).Returns(callInfo =>
        {
            _listSubject.OnNext(callInfo.Arg<DanceList>()!);
            return Task.CompletedTask;
        });

        _sut = new DanceListStepViewModel(_store, new NoOpLoggerService(),
            Substitute.For<INotificationService>(), _confirmations);
    }

    [Fact]
    public void StartsUnanswered() => Assert.False(_sut.HasAnswered);

    [Fact]
    public async Task StartEmpty_OnAFreshProfile_AnswersWithoutAsking()
    {
        await _sut.StartEmptyCommand.Execute();

        Assert.True(_sut.HasAnswered);
        await _confirmations.DidNotReceiveWithAnyArgs()
            .ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task StartEmpty_WithAList_ActuallyEmptiesItOnceConfirmed()
    {
        _listSubject.OnNext(TestData.CreateSimpleDanceList());
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        await _sut.StartEmptyCommand.Execute();

        // The button says the list will be empty, so the list is empty. Leaving it alone is what
        // made this look like a button that did nothing.
        Assert.True(_store.Current.IsEmpty);
        Assert.True(_sut.HasAnswered);
    }

    [Fact]
    public async Task StartEmpty_Declined_ChangesNothing()
    {
        _listSubject.OnNext(TestData.CreateSimpleDanceList());
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        await _sut.StartEmptyCommand.Execute();

        Assert.Equal(3, _store.Current.AllDances.Count());
        Assert.False(_sut.HasAnswered);
    }

    [Fact]
    public async Task StartEmpty_SaysSoInTheSummary()
    {
        await _sut.StartEmptyCommand.Execute();

        Assert.False(string.IsNullOrEmpty(_sut.Summary));
    }

    [Fact]
    public async Task EnterAsync_WithAnExistingList_CountsAsAnswered()
    {
        _listSubject.OnNext(TestData.CreateSimpleDanceList());

        await _sut.EnterAsync();

        Assert.True(_sut.HasAnswered);
        Assert.Contains("3", _sut.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnterAsync_WithNoList_LeavesTheStepUnanswered()
    {
        await _sut.EnterAsync();

        Assert.False(_sut.HasAnswered);
    }

    [Fact]
    public async Task ImportAsync_MissingFile_ReportsRatherThanThrows()
    {
        var missing = new FileInfo(Path.Combine(Path.GetTempPath(), $"r4b_{Guid.NewGuid():N}.json"));

        await _sut.ImportAsync(missing);

        Assert.False(_sut.HasAnswered);
    }

    public void Dispose() => _listSubject.Dispose();
}
