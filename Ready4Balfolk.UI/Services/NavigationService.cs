using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace Ready4Balfolk.UI.Services;

public enum Screen
{
    Main,
    Settings,
    Help,
    Tagging,

    /// <summary>Advanced discovery settings, with the previews that make declaring one informed.</summary>
    Discovery,

    /// <summary>First-run setup. A screen, not a dialog: its steps need the whole window.</summary>
    Setup
}

public sealed partial class NavigationService : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    [Reactive] public partial Screen CurrentScreen { get; set; }
    [Reactive] public partial bool IsHistoryMode { get; set; }
    [Reactive] public partial bool IsDanceListMode { get; set; }

    [ObservableAsProperty] public partial bool IsMainScreen { get; }
    [ObservableAsProperty] public partial bool IsSettingsScreen { get; }
    [ObservableAsProperty] public partial bool IsHelpScreen { get; }
    [ObservableAsProperty] public partial bool IsTaggingScreen { get; }
    [ObservableAsProperty] public partial bool IsDiscoveryScreen { get; }
    [ObservableAsProperty] public partial bool IsSetupScreen { get; }

    public NavigationService()
    {
        _isMainScreenHelper = this.WhenAnyValue(x => x.CurrentScreen)
            .Select(s => s == Screen.Main)
            .ToProperty(this, x => x.IsMainScreen);
        _isMainScreenHelper.DisposeWith(_disposables);

        _isSettingsScreenHelper = this.WhenAnyValue(x => x.CurrentScreen)
            .Select(s => s == Screen.Settings)
            .ToProperty(this, x => x.IsSettingsScreen);
        _isSettingsScreenHelper.DisposeWith(_disposables);

        _isHelpScreenHelper = this.WhenAnyValue(x => x.CurrentScreen)
            .Select(s => s == Screen.Help)
            .ToProperty(this, x => x.IsHelpScreen);
        _isHelpScreenHelper.DisposeWith(_disposables);

        _isTaggingScreenHelper = this.WhenAnyValue(x => x.CurrentScreen)
            .Select(s => s == Screen.Tagging)
            .ToProperty(this, x => x.IsTaggingScreen);
        _isTaggingScreenHelper.DisposeWith(_disposables);

        _isDiscoveryScreenHelper = this.WhenAnyValue(x => x.CurrentScreen)
            .Select(s => s == Screen.Discovery)
            .ToProperty(this, x => x.IsDiscoveryScreen);
        _isDiscoveryScreenHelper.DisposeWith(_disposables);

        _isSetupScreenHelper = this.WhenAnyValue(x => x.CurrentScreen)
            .Select(s => s == Screen.Setup)
            .ToProperty(this, x => x.IsSetupScreen);
        _isSetupScreenHelper.DisposeWith(_disposables);
    }

    public void Dispose() => _disposables.Dispose();
}
