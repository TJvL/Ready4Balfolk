using System;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.UI.Views.Equalizer;
using Ready4Balfolk.UI.Views.Help;
using Ready4Balfolk.UI.Views.History;
using Ready4Balfolk.UI.Views.Playback;
using Ready4Balfolk.UI.Views.Queue;
using Ready4Balfolk.UI.Views.Settings;
using Ready4Balfolk.UI.Views.Toolbar;
using Ready4Balfolk.UI.Views.TrackCatalog;

namespace Ready4Balfolk.UI;

public sealed partial class MainWindowViewModel : ReactiveObject
{
    public NavigationService Navigation { get; }
    public ToolbarViewModel Toolbar { get; }
    public PlaybackViewModel Playback { get; }
    public EqualizerViewModel Equalizer { get; }
    public QueueViewModel Queue { get; }
    public TrackCatalogViewModel TrackCatalog { get; }

    [Reactive] public partial HistoryViewModel? History { get; set; }
    [Reactive] public partial DanceListViewModel? DanceList { get; set; }
    [Reactive] public partial SettingsViewModel? Settings { get; set; }
    [Reactive] public partial HelpViewModel? Help { get; set; }

    public MainWindowViewModel(
        NavigationService navigation,
        ToolbarViewModel toolbar,
        PlaybackViewModel playback,
        EqualizerViewModel equalizer,
        QueueViewModel queue,
        TrackCatalogViewModel trackCatalog,
        Lazy<HistoryViewModel> lazyHistory,
        Lazy<DanceListViewModel> lazyDanceList,
        Lazy<SettingsViewModel> lazySettings,
        Lazy<HelpViewModel> lazyHelp)
    {
        Navigation = navigation;
        Toolbar = toolbar;
        Playback = playback;
        Equalizer = equalizer;
        Queue = queue;
        TrackCatalog = trackCatalog;

        // Defer secondary-screen ViewModels until first navigation
        navigation.WhenAnyValue(x => x.CurrentScreen)
            .Subscribe(screen =>
            {
                if (screen is Screen.Settings && Settings is null)
                {
                    Settings = lazySettings.Value;
                }
                else if (screen is Screen.Help && Help is null)
                {
                    Help = lazyHelp.Value;
                }
            });

        // Defer main-screen toggle ViewModels until first toggle
        navigation.WhenAnyValue(x => x.IsHistoryMode)
            .Where(active => active)
            .Take(1)
            .Subscribe(_ => History = lazyHistory.Value);

        navigation.WhenAnyValue(x => x.IsDanceListMode)
            .Where(active => active)
            .Take(1)
            .Subscribe(_ => DanceList = lazyDanceList.Value);
    }
}
