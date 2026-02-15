using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceSynonyms;
using Ready4Balfolk.UI.Views.DanceTree;
using Ready4Balfolk.UI.Views.History;
using Ready4Balfolk.UI.Views.Playback;
using Ready4Balfolk.UI.Views.Queue;
using Ready4Balfolk.UI.Views.Settings;
using Ready4Balfolk.UI.Views.Toolbar;
using Ready4Balfolk.UI.Views.TrackCatalog;

namespace Ready4Balfolk.UI;

public sealed class MainWindowViewModel(
    NavigationService navigation,
    ToolbarViewModel toolbar,
    PlaybackViewModel playback,
    QueueViewModel queue,
    HistoryViewModel history,
    TrackCatalogViewModel trackCatalog,
    DanceTreeViewModel danceTree,
    SettingsViewModel settings,
    DanceSynonymsViewModel danceSynonyms)
{
    public NavigationService Navigation { get; } = navigation;
    public ToolbarViewModel Toolbar { get; } = toolbar;
    public PlaybackViewModel Playback { get; } = playback;
    public QueueViewModel Queue { get; } = queue;
    public HistoryViewModel History { get; } = history;
    public TrackCatalogViewModel TrackCatalog { get; } = trackCatalog;
    public DanceTreeViewModel DanceTree { get; } = danceTree;
    public SettingsViewModel Settings { get; } = settings;
    public DanceSynonymsViewModel DanceSynonyms { get; } = danceSynonyms;
}
