using System.Threading.Tasks;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's second step: where the music is.</summary>
/// <remarks>
/// Not required to move on. A user who has not decided yet is better served by an application they
/// can look at than by a wizard that will not let them past, and the same picker sits in settings.
/// </remarks>
public sealed partial class MusicDirectoryStepViewModel(ISettingsStore settingsStore) : WizardStepViewModel
{
    /// <summary>Nullable because nothing has been picked yet on a fresh profile.</summary>
    [Reactive] public partial string? MusicDirectoryPath { get; set; }

    public override string Title => UiStrings.Wizard_Music_Title;

    public override string Explanation => UiStrings.Wizard_Music_Explanation;

    public override Task EnterAsync()
    {
        MusicDirectoryPath = settingsStore.Current.MusicDirectoryPath;
        return Task.CompletedTask;
    }

    public override async Task<bool> CommitAsync()
    {
        await settingsStore.UpdateAsync(settings => settings with
        {
            MusicDirectoryPath = MusicDirectoryPath ?? string.Empty
        });

        return true;
    }
}
