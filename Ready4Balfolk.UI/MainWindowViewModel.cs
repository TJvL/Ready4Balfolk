using System;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.UI.Views.Discovery;
using Ready4Balfolk.UI.Views.Review;
using Ready4Balfolk.UI.Views.Equalizer;
using Ready4Balfolk.UI.Views.Help;
using Ready4Balfolk.UI.Views.History;
using Ready4Balfolk.UI.Views.Playback;
using Ready4Balfolk.UI.Views.Queue;
using Ready4Balfolk.UI.Views.Settings;
using Ready4Balfolk.UI.Views.Tagging;
using Ready4Balfolk.UI.Views.Toolbar;
using Ready4Balfolk.UI.Views.TrackCatalog;
using Ready4Balfolk.UI.Views.Wizard;

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
    [Reactive] public partial TaggingViewModel? Tagging { get; set; }
    [Reactive] public partial DiscoveryViewModel? Discovery { get; set; }
    [Reactive] public partial ReviewViewModel? Review { get; set; }
    [Reactive] public partial SetupWizardViewModel? Setup { get; set; }

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
        Lazy<HelpViewModel> lazyHelp,
        Lazy<TaggingViewModel> lazyTagging,
        Lazy<DiscoveryViewModel> lazyDiscovery,
        Lazy<ReviewViewModel> lazyReview,
        Func<SetupWizardViewModel> setupFactory)
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
                else if (screen is Screen.Setup)
                {
                    // Built fresh each time, so running setup again starts from what is on disk
                    // rather than from where the last visit left off.
                    Setup = setupFactory();
                }
                else if (screen is Screen.Tagging)
                {
                    Tagging ??= lazyTagging.Value;
                    // Rebuilt on every visit: the index changes underneath it whenever a scan runs.
                    Tagging.RefreshCommand.Execute().Subscribe();
                }
                else if (screen is Screen.Discovery)
                {
                    Discovery ??= lazyDiscovery.Value;
                    // Measured against the library as it is now, for the same reason.
                    Discovery.RefreshCommand.Execute().Subscribe();
                }
                else if (screen is Screen.Review)
                {
                    Review ??= lazyReview.Value;
                    // Rebuilt on every visit, which is what makes the queue resumable: it is
                    // derived from the index rather than remembered from the last time.
                    Review.RefreshCommand.Execute().Subscribe();
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
