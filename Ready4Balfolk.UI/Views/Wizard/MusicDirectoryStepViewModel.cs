using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's step for where the music is.</summary>
/// <remarks>
/// Required. Everything after this reads the library, and there is nothing to set up without one:
/// letting a user past here only produces an application that appears to work and finds nothing.
/// </remarks>
public sealed partial class MusicDirectoryStepViewModel(ISettingsStore settingsStore) : WizardStepViewModel
{
    /// <summary>Nullable because nothing has been picked yet on a fresh profile.</summary>
    [Reactive] public partial string? MusicDirectoryPath { get; set; }

    public override string Title => UiStrings.Wizard_Music_Title;

    public override string Explanation => UiStrings.Wizard_Music_Explanation;

    public override IObservable<bool> CanContinue =>
        this.WhenAnyValue(x => x.MusicDirectoryPath)
            .Select(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path));

    public override string BlockedReason => UiStrings.Wizard_Music_Required;

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
