using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Ready4Balfolk.UI.Services;

public enum Screen
{
    Main,
    Settings,
    Help,
    Synonyms
}

public sealed partial class NavigationService : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    [Reactive] public partial Screen CurrentScreen { get; set; }
    [Reactive] public partial bool IsHistoryMode { get; set; }
    [Reactive] public partial bool IsTreeViewMode { get; set; }

    [ObservableAsProperty] public partial bool IsMainScreen { get; }
    [ObservableAsProperty] public partial bool IsSettingsScreen { get; }
    [ObservableAsProperty] public partial bool IsHelpScreen { get; }
    [ObservableAsProperty] public partial bool IsSynonymsScreen { get; }

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

        _isSynonymsScreenHelper = this.WhenAnyValue(x => x.CurrentScreen)
            .Select(s => s == Screen.Synonyms)
            .ToProperty(this, x => x.IsSynonymsScreen);
        _isSynonymsScreenHelper.DisposeWith(_disposables);
    }

    public void Dispose() => _disposables.Dispose();
}
