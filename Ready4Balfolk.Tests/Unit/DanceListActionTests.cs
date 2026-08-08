using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DanceListActionTests
{
    private readonly IDanceListStore _store = Substitute.For<IDanceListStore>();
    private DanceList _current = TestData.CreateSimpleDanceList();

    public DanceListActionTests()
    {
        _store.Current.Returns(_ => _current);
        _store.Index.Returns(_ => DanceListIndex.Build(_current));
        _store.UpdateAsync(Arg.Any<Func<DanceList, DanceList>>())
            .Returns(callInfo =>
            {
                // Arg<T> is unconstrained, so the compiler reads it as possibly null; the call it
                // matched always supplied a transform.
                var transform = callInfo.Arg<Func<DanceList, DanceList>>()!;
                _current = transform(_current);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task AddName_TakenByAnotherDance_IsRefusedAndSaysWhichOne()
    {
        var result = await DanceListAction.AddName(_store, "plinn", "Mazurk").ExecuteAsync();

        Assert.False(result.Success);
        Assert.Contains("Mazurka", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(["Plinn"], _current.AllDances.First(d => d.Slug == "plinn").Names);
    }

    [Fact]
    public async Task AddName_FreeName_IsAccepted()
    {
        var result = await DanceListAction.AddName(_store, "plinn", "Ton plinn").ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal(["Plinn", "Ton plinn"], _current.AllDances.First(d => d.Slug == "plinn").Names);
    }

    [Fact]
    public async Task AddName_TheDanceAlreadyHas_IsAllowedThrough()
    {
        // Not a collision: the name already belongs to this dance, so nothing becomes ambiguous.
        var result = await DanceListAction.AddName(_store, "mazurka", "Mazurk").ExecuteAsync();

        Assert.True(result.Success);
    }

    [Fact]
    public async Task AddName_Blank_IsRefused()
    {
        var result = await DanceListAction.AddName(_store, "plinn", "   ").ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddDance_NameTakenElsewhere_IsRefused()
    {
        var result = await DanceListAction.AddDance(_store, [1], "Schottische").ExecuteAsync();

        Assert.False(result.Success);
        Assert.Equal(3, _current.AllDances.Count());
    }

    [Fact]
    public async Task AddDance_AtTheRoot_IsRefused()
    {
        var result = await DanceListAction.AddDance(_store, [], "Andro").ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameCategory_ToASiblingsName_IsRefused()
    {
        var result = await DanceListAction.RenameCategory(_store, [1], "Common").ExecuteAsync();

        Assert.False(result.Success);
        Assert.Equal("Bretagne", _current.Categories[1].Name);
    }

    [Fact]
    public async Task RenameCategory_ToItsOwnName_IsAllowed()
    {
        var result = await DanceListAction.RenameCategory(_store, [1], "Bretagne").ExecuteAsync();

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ReweightDance_Negative_IsRefused()
    {
        var result = await DanceListAction.ReweightDance(_store, "plinn", -1).ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ReweightDance_Zero_IsAllowed()
    {
        // Zero is a real answer: never pick this one.
        var result = await DanceListAction.ReweightDance(_store, "plinn", 0).ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal(0, _current.AllDances.First(d => d.Slug == "plinn").Weight);
    }

    [Fact]
    public async Task RemoveName_TheLastOne_IsRefused()
    {
        var result = await DanceListAction.RemoveName(_store, "plinn", 0).ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Undo_PutsTheWholeListBack()
    {
        var action = DanceListAction.DeleteCategory(_store, [0]);
        await action.ExecuteAsync();
        Assert.Single(_current.Categories);

        await action.UndoAsync();

        Assert.Equal(2, _current.Categories.Count);
        Assert.Equal(3, _current.AllDances.Count());
    }

    [Fact]
    public async Task ARefusedActionDoesNotBecomeUndoable()
    {
        var action = DanceListAction.AddName(_store, "plinn", "Mazurk");
        await action.ExecuteAsync();

        // Undoing a refused action would restore the empty "before" it never captured.
        await action.UndoAsync();

        Assert.Equal(3, _current.AllDances.Count());
    }

    [Fact]
    public void DeleteDance_DescribesItselfWithTheDisplayedName()
    {
        Assert.Contains("Mazurka", DanceListAction.DeleteDance(_store, "mazurka").Description,
            StringComparison.Ordinal);
    }
}
