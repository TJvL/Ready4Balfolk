using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Dialogs.Confirmation;
using Ready4Balfolk.UI.Views.Presentation;
using Ready4Balfolk.Web;
using AvaloniaWindowState = Avalonia.Controls.WindowState;
using DomainWindowState = Ready4Balfolk.Domain.Models.Settings.WindowState;

namespace Ready4Balfolk.UI;

/// <summary>What the application does between the window existing and being usable.</summary>
/// <remarks>
/// This was the body of <see cref="App.OnFrameworkInitializationCompleted"/>, about a hundred and
/// fifty lines reaching into the container for each thing it needed as it went. Everything it
/// depends on now arrives in the constructor, which is what makes it something other than a method
/// that can only be run by starting a desktop application.
/// </remarks>
internal sealed class ApplicationStartup(
    ISettingsStore settingsStore,
    ILoggerService logger,
    INotificationService notifications,
    IConfirmationService confirmations,
    ITrackStore trackStore,
    IDanceListStore danceListStore,
    ILibraryIndex libraryIndex,
    IQueueHistoryStore historyStore,
    PresentationWebServer webServer,
    NavigationService navigation,
    ConfirmationService confirmationOwner,
    FilePickerService pickers,
    TrackEditorService trackEditor,
    TimeProvider time) : IDisposable
{
    /// <summary>How long a silence has to be before it stops being the same evening.</summary>
    /// <remarks>
    /// A gap rather than a date, because a ball crossing midnight is normal and a night that ran
    /// until two would otherwise be two nights.
    /// </remarks>
    private static readonly TimeSpan UnfinishedNightGap = TimeSpan.FromHours(8);

    private readonly List<PresentationWindow> _presentationWindows = [];
    private readonly CompositeDisposable _disposables = [];
    private bool _closing;

    /// <summary>The screens currently up for the room.</summary>
    /// <remarks>
    /// Internal, for the scenario runs: a presentation window is not owned by the main window and
    /// the headless platform keeps no list of its own, so there is otherwise no way to ask what the
    /// dancers are being shown.
    /// </remarks>
    internal IReadOnlyList<PresentationWindow> PresentationWindows => _presentationWindows;

    /// <summary>Builds the main window and wires everything that follows from it.</summary>
    public void Run(IClassicDesktopStyleApplicationLifetime desktop, IApplicationAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(appearance);

        var mainWindow = new MainWindow();
        desktop.MainWindow = mainWindow;

        confirmationOwner.SetOwner(mainWindow);
        pickers.SetOwner(mainWindow);
        trackEditor.SetOwner(mainWindow);

        mainWindow.Opened += (_, _) => OnMainWindowOpened(mainWindow, appearance);

        _disposables.Add(Observable.FromEventPattern(
                h => mainWindow.Opened += h,
                h => mainWindow.Opened -= h)
            .Take(1)
            .SelectMany(_ =>
                Observable.Merge(
                    RunLoad(token => danceListStore.LoadAsync(token), "Failed to load the dance list"),
                    // Opened before anything asks it a question: the track store reads it on the
                    // first music directory it is handed, which can be immediately.
                    RunLoad(token => libraryIndex.OpenAsync(token), "Failed to open the library index"),
                    RunLoad(token => historyStore.LoadAsync(token), "Failed to load queue history")
                // ToList waits for every load to finish before emitting once. The wizard reads the
                // dance list to decide what to show, so it cannot open while that load is still in
                // flight, or a profile that has a list looks like a fresh one.
                ).ToList())
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ =>
            {
                ShowSetupIfNeeded();
                AskAboutUnfinishedNightAsync().SafeFireAndForget(exception =>
                    logger.ErrorAsync("Failed to ask about an unfinished night", exception));
            }));

        mainWindow.Closing += (_, e) =>
        {
            // The smoke test shuts the app down itself and there is nobody there to answer a
            // confirmation dialog, so the close has to go straight through.
            if (_closing || Program.IsSmokeTest)
            {
                return;
            }

            // Cancel the close; HandleClosingAsync closes the window for real (with _closing set)
            // once the confirmation dialog and state saving are done.
            e.Cancel = true;

            HandleClosingAsync(mainWindow).SafeFireAndForget(exception =>
                logger.ErrorAsync("Failed to handle window closing", exception));
        };
    }

    private void OnMainWindowOpened(MainWindow mainWindow, IApplicationAppearance appearance)
    {
        _ = logger.InfoAsync($"Window opened in {Program.StartupStopwatch.ElapsedMilliseconds} ms");

        _disposables.Add(logger.WhenErrorLogged
            .GroupBy(entry => entry.Message)
            .SelectMany(group => group.Throttle(TimeSpan.FromSeconds(2)))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(entry => notifications.Show(entry.Message, NotificationSeverity.Error)));

        appearance.ApplyShowButtonText(settingsStore.Current.ShowButtonText);
        _disposables.Add(settingsStore.Observe()
            .Select(s => s.ShowButtonText)
            .DistinctUntilChanged()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(appearance.ApplyShowButtonText));

        appearance.ApplyTheme(settingsStore.Current.ApplicationTheme);
        _disposables.Add(settingsStore.Observe()
            .Select(s => s.ApplicationTheme)
            .DistinctUntilChanged()
            .Subscribe(appearance.ApplyTheme));

        // One subscription, one value. These used to be three separate subscriptions into three
        // setters, and the order they were declared in mattered.
        _disposables.Add(settingsStore.Observe()
            .Select(s => new TrackLibraryConfiguration(
                s.MusicDirectoryPath, s.Discovery, s.AllowDancesOutsideTheList))
            .DistinctUntilChanged()
            .Subscribe(configuration => trackStore.ApplyAsync(configuration)
                .SafeFireAndForget(exception =>
                    logger.ErrorAsync("Failed to apply the library settings", exception))));

        RestoreWindowState(mainWindow);

        // The embedded server follows the settings live: switching it on, moving its port or
        // opening it to the network never needs a restart.
        ApplyWebServer(settingsStore.Current);
        _disposables.Add(settingsStore.Observe()
            .Select(ToWebServerOptions)
            .DistinctUntilChanged()
            .Subscribe(options => webServer.ApplyAsync(options).SafeFireAndForget(exception =>
                logger.ErrorAsync("Failed to apply presentation server settings", exception))));

        SyncPresentationWindows(settingsStore.Current.PresentationDisplayCount);
        _disposables.Add(settingsStore.Observe()
            .Select(s => s.PresentationDisplayCount)
            .DistinctUntilChanged()
            .Subscribe(SyncPresentationWindows));
    }

    private void RestoreWindowState(Window mainWindow)
    {
        var windowState = settingsStore.Current.MainWindowState;

        if (windowState is { X: not null, Y: not null })
        {
            mainWindow.Position = new PixelPoint((int)windowState.X.Value, (int)windowState.Y.Value);
        }

        if (windowState is { Width: not null, Height: not null })
        {
            mainWindow.Width = windowState.Width.Value;
            mainWindow.Height = windowState.Height.Value;
        }

        if (windowState.IsMaximized)
        {
            mainWindow.WindowState = AvaloniaWindowState.Maximized;
        }
    }

    private async Task HandleClosingAsync(MainWindow mainWindow)
    {
        var dialogVm = new ConfirmationDialogViewModel
        {
            Title = UiStrings.App_ExitTitle,
            Message = UiStrings.App_ExitMessage
        };
        var dialog = new ConfirmationDialogView { DataContext = dialogVm };
        await dialog.ShowDialog(mainWindow);

        if (dialogVm.DialogResult != true)
        {
            return;
        }

        // Saved while everything is still open, because a closed window has no bounds to read.
        var isMaximized = mainWindow.WindowState == AvaloniaWindowState.Maximized;
        var bounds = mainWindow.Bounds;
        var position = mainWindow.Position;

        var presentationStates = _presentationWindows.Select(window => new DomainWindowState(
            window.Position.X,
            window.Position.Y,
            window.Bounds.Width,
            window.Bounds.Height,
            window.WindowState == AvaloniaWindowState.Maximized,
            window.IsBorderless)).ToList();

        await settingsStore.UpdateAsync(s => s with
        {
            MainWindowState = new DomainWindowState(
                position.X, position.Y, bounds.Width, bounds.Height, isMaximized),
            PresentationWindowStates = presentationStates
        });

        // Asked to stop, never waited for. The process is about to end and the socket goes with it,
        // so there is nothing here worth making the user watch: awaiting Kestrel's drain is what
        // made the close button appear to do nothing while a browser held a WebSocket open.
        webServer.DisposeAsync().AsTask().SafeFireAndForget(exception =>
            logger.ErrorAsync("Presentation server did not shut down cleanly", exception));

        _disposables.Dispose();

        foreach (var window in _presentationWindows)
        {
            window.AllowClose = true;
            window.Close();
        }

        _presentationWindows.Clear();

        // Now close for real; the second Closing invocation passes through.
        _closing = true;
        mainWindow.Close();
    }

    /// <summary>Sends a profile that has never been through setup to it.</summary>
    /// <remarks>
    /// Not for the smoke test: it drives the application with nobody there to answer a wizard, and
    /// it would simply sit on the first step until CI gave up.
    /// </remarks>
    public void ShowSetupIfNeeded()
    {
        if (Program.IsSmokeTest || settingsStore.Current.SetupCompleted)
        {
            return;
        }

        ShowSetup();
    }

    public void ShowSetup() => navigation.CurrentScreen = Screen.Setup;

    /// <summary>Asks once, about an evening that was never ended.</summary>
    /// <remarks>
    /// The application was closed, the laptop went flat, the night simply stopped. Startup is the
    /// one place this can be asked without interrupting a room, and neither answer deletes anything.
    /// Not for the smoke test, for the same reason the wizard is not: nobody is there to answer.
    /// </remarks>
    private async Task AskAboutUnfinishedNightAsync()
    {
        if (Program.IsSmokeTest)
        {
            return;
        }

        var night = historyStore.Current;
        if (night.Entries.Count == 0 || !night.IsOpen || night.LastActivityAt is not { } lastActivity)
        {
            return;
        }

        if (time.GetLocalNow().DateTime - lastActivity < UnfinishedNightGap)
        {
            return;
        }

        var message = string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.App_UnfinishedNightMessage,
            lastActivity.ToString("d MMMM", CultureInfo.CurrentCulture),
            night.Entries.Count);

        var startFresh = await confirmations.ConfirmAsync(
            UiStrings.App_UnfinishedNightTitle,
            message,
            UiStrings.App_UnfinishedNightStartFresh,
            UiStrings.App_UnfinishedNightCarryOn);

        if (startFresh)
        {
            // Ended when it stopped, not when it was noticed: the question is asked at the next
            // start, which can be days later, and an evening that reads as having run until
            // Tuesday is not the evening anybody had.
            await historyStore.EndNightAsync(lastActivity);
        }
    }

    private static WebServerOptions ToWebServerOptions(ApplicationSettings settings) => new(
        settings.WebServerEnabled,
        settings.WebServerPortClamped,
        settings.WebRemoteControlEnabled,
        settings.WebRemoteControlPin);

    private void ApplyWebServer(ApplicationSettings settings) =>
        webServer.ApplyAsync(ToWebServerOptions(settings)).SafeFireAndForget(exception =>
            logger.ErrorAsync("Failed to start the presentation server", exception));

    private IObservable<Unit> RunLoad(Func<CancellationToken, Task> loader, string errorMessage) =>
        Observable.Defer(() => Observable.FromAsync(loader)
            .TimeInterval()
            .Do(interval => logger.InfoAsync($"Load completed | Duration: {interval.Interval:g}"))
            .Select(interval => interval.Value)
            .SubscribeOn(RxSchedulers.TaskpoolScheduler)
            .Catch<Unit, Exception>(exception =>
            {
                _ = logger.ErrorAsync(errorMessage, exception);
                // Just continue.
                return Observable.Empty<Unit>();
            }));

    private void SyncPresentationWindows(int targetCount)
    {
        targetCount = Math.Clamp(targetCount, 0, 10);

        while (_presentationWindows.Count > targetCount)
        {
            var last = _presentationWindows[^1];
            _presentationWindows.RemoveAt(_presentationWindows.Count - 1);
            last.AllowClose = true;
            last.Close();
        }

        var savedStates = settingsStore.Current.PresentationWindowStates.ToList();
        while (_presentationWindows.Count < targetCount)
        {
            var index = _presentationWindows.Count;
            var window = new PresentationWindow
            {
                WindowIndex = index,
                Title = string.Format(CultureInfo.CurrentCulture, UiStrings.Presentation_WindowTitle, index + 1)
            };

            // Restored after the window is shown, so the window manager respects the position.
            if (index < savedStates.Count)
            {
                var state = savedStates[index];
                window.Opened += (_, _) => RestorePresentationWindow(window, state);
            }

            window.Show();
            _presentationWindows.Add(window);
        }
    }

    private static void RestorePresentationWindow(PresentationWindow window, DomainWindowState state)
    {
        if (state is { X: not null, Y: not null })
        {
            window.Position = new PixelPoint((int)state.X.Value, (int)state.Y.Value);
        }

        if (state is { Width: not null, Height: not null })
        {
            window.Width = state.Width.Value;
            window.Height = state.Height.Value;
        }

        if (state.IsBorderless)
        {
            window.IsBorderless = true;
        }
        else if (state.IsMaximized)
        {
            window.WindowState = AvaloniaWindowState.Maximized;
        }
    }

    public void Dispose() => _disposables.Dispose();
}

/// <summary>The two things startup changes on the Application object itself.</summary>
/// <remarks>
/// Both reach into Avalonia's own state, the resource dictionary and the requested theme variant,
/// which only <see cref="App"/> has. This is the seam that keeps the rest of startup free of it.
/// </remarks>
internal interface IApplicationAppearance
{
    void ApplyShowButtonText(bool showText);

    void ApplyTheme(ApplicationTheme theme);
}
