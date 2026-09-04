using System;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Dialogs.QrCode;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.UI.Views.Toolbar;

/// <summary>The toolbar, and the one place a scan is allowed to mention what it could not place.</summary>
/// <remarks>
/// A count on a button, never a dialog and never a toast. New files arrive while the application is
/// running in front of a room, and a tagging question during a bal is the worst possible moment to
/// ask one. The count is a query against the index, so it survives a restart for free.
/// </remarks>
public sealed partial class ToolbarViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = [];
    private readonly PresentationWebServer _webServer;
    private readonly ISettingsStore _settingsStore;

    /// <summary>How many tracks are waiting for a person, which is what the gate holds back.</summary>
    [Reactive] public partial int InReviewCount { get; private set; }
    [Reactive] public partial string InReviewText { get; private set; }
    [Reactive] public partial bool HasInReview { get; private set; }

    /// <summary>
    /// Whether each page is actually being served, which is not the same as the switch being on.
    /// </summary>
    /// <remarks>
    /// Read off the server rather than off the settings, the way the settings status line already
    /// is: a port somebody else has taken fails the start, and a toolbar that said "display" over a
    /// server that never bound would send a hall to an address nothing answers.
    /// </remarks>
    [Reactive] public partial bool IsServingDisplay { get; private set; }

    [Reactive] public partial bool IsServingRemote { get; private set; }

    public ToolbarViewModel(ITrackStore trackStore, PresentationWebServer webServer, ISettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(webServer);
        ArgumentNullException.ThrowIfNull(settingsStore);

        _webServer = webServer;
        _settingsStore = settingsStore;
        InReviewText = string.Empty;

        UpdateWhatIsServed();
        webServer.WhenChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateWhatIsServed())
            .DisposeWith(_disposables);

        settingsStore.Observe()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateWhatIsServed())
            .DisposeWith(_disposables);

        // The gate's own number, pushed whenever the library is rebuilt. A SQL count once decided
        // this independently and missed two of the gate's three reasons to hold a track back.
        trackStore.InReviewCount
            .DistinctUntilChanged()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(waiting =>
            {
                InReviewCount = waiting;
                HasInReview = waiting > 0;
                InReviewText = waiting > 0
                    ? string.Format(CultureInfo.CurrentCulture, UiStrings.Toolbar_ReviewCount, waiting)
                    : string.Empty;
            })
            .DisposeWith(_disposables);
    }

    /// <summary>What a phone would be pointed at for the display page.</summary>
    public QrCodeDialogViewModel? DisplayAddress() => Address(UiStrings.Qr_DisplayTitle, string.Empty, null);

    /// <summary>The same for the remote, which also needs the PIN to be any use.</summary>
    public QrCodeDialogViewModel? RemoteAddress() => Address(
        UiStrings.Qr_RemoteTitle, "/remote", _settingsStore.Current.WebRemoteControlPin);

    private QrCodeDialogViewModel? Address(string title, string path, string? pin)
    {
        var addresses = _webServer.Addresses;

        return addresses.Count == 0
            ? null
            : new QrCodeDialogViewModel(
                title,
                addresses[0] + path,
                pin,
                [.. addresses.Select(address => address + path)]);
    }

    private void UpdateWhatIsServed()
    {
        var running = _webServer.State is WebServerState.Running;

        IsServingDisplay = running;
        IsServingRemote = running && _settingsStore.Current.WebRemoteControlEnabled;
    }

    public void Dispose() => _disposables.Dispose();
}
