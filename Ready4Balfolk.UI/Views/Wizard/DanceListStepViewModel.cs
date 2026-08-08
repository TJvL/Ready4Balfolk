using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
// Views.DanceList is a sibling namespace of this one, so the model needs a name of its own here.
using DanceListModel = Ready4Balfolk.Domain.Models.Dances.DanceList;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's first step: building the dance list.</summary>
/// <remarks>
/// Nothing ships with the application, so this is where the list comes into existence. Either the
/// user imports the one BigBalfolkList publishes, or they start empty and add dances by hand on the
/// dance list screen. Both are real answers, so neither is offered as the lesser one.
/// </remarks>
public sealed partial class DanceListStepViewModel(
    IDanceListStore store,
    ILoggerService loggerService,
    INotificationService notifications,
    IConfirmationService confirmations) : WizardStepViewModel
{
    /// <summary>Where a list to import is published. Opened in the user's own browser.</summary>
    public const string SourceUrl = "https://github.com/TJvL/BigBalfolkList";

    /// <summary>
    /// True once the user has actually answered, by importing or by choosing to start empty. An
    /// empty list is a legitimate answer but an unanswered step is not, so the wizard waits.
    /// </summary>
    [Reactive] public partial bool HasAnswered { get; private set; }

    /// <summary>What the list looks like now, or null before the step has been answered.</summary>
    [Reactive] public partial string? Summary { get; private set; }

    public override string Title => UiStrings.Wizard_DanceList_Title;

    public override string Explanation => UiStrings.Wizard_DanceList_Explanation;

    public override IObservable<bool> CanContinue => this.WhenAnyValue(x => x.HasAnswered);

    public override Task EnterAsync()
    {
        // Re-running the wizard on a profile that already has a list must not read as a fresh
        // start, or the obvious move is to import again over a list the user has since edited.
        if (!store.Current.IsEmpty)
        {
            HasAnswered = true;
            Summary = DescribeCurrentList();
        }

        return Task.CompletedTask;
    }

    /// <summary>Reads a BigBalfolkList export and makes it the user's list.</summary>
    public async Task ImportAsync(FileInfo fileInfo)
    {
        try
        {
            var list = await BigBalfolkListImporter.ReadAsync(fileInfo);
            await store.ReplaceAsync(list);

            HasAnswered = true;
            Summary = DescribeCurrentList();
            await loggerService.InfoAsync(
                $"Setup imported {list.AllDances.Count()} dances from {fileInfo.FullName}");
        }
        catch (Exception exception) when (exception is InvalidDataException or FileNotFoundException)
        {
            // A refused file is a message, not an error: the import said exactly what was wrong
            // with it and the user can pick a different one.
            notifications.Show(exception.Message, NotificationSeverity.Warning);
        }
        catch (IOException exception)
        {
            await loggerService.ErrorAsync("Failed to import a dance list", exception);
            notifications.Show(UiStrings.Wizard_DanceList_ImportFailed, NotificationSeverity.Error);
        }
    }

    /// <summary>
    /// Empties the list, for a user who would rather add only the dances they actually play.
    /// </summary>
    /// <remarks>
    /// It really does empty it. Treating this as "do not import" instead would make the button do
    /// nothing at all on a profile that already has a list, which is exactly what it looks like it
    /// should undo.
    /// </remarks>
    [ReactiveCommand]
    private async Task StartEmptyAsync()
    {
        if (!store.Current.IsEmpty)
        {
            var message = string.Format(
                CultureInfo.CurrentCulture,
                UiStrings.Wizard_DanceList_ClearConfirmMessage,
                store.Current.AllDances.Count());

            if (!await confirmations.ConfirmAsync(
                    UiStrings.Wizard_DanceList_ClearTitle,
                    message,
                    UiStrings.Wizard_DanceList_ClearConfirm,
                    UiStrings.Wizard_DanceList_ClearCancel))
            {
                return;
            }

            await store.ReplaceAsync(DanceListModel.Empty);
            await loggerService.InfoAsync("Setup emptied the dance list");
        }

        HasAnswered = true;
        Summary = UiStrings.Wizard_DanceList_EmptySummary;
    }

    private string DescribeCurrentList()
    {
        var list = store.Current;
        return string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.Wizard_DanceList_SummaryFormat,
            list.AllDances.Count(),
            CountCategories(list.Categories));
    }

    private static int CountCategories(IReadOnlyList<DanceCategory> categories) =>
        categories.Sum(category => 1 + CountCategories(category.Categories));
}
