using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
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

public sealed class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    private readonly List<PresentationWindow> _presentationWindows = [];
    private readonly CompositeDisposable _compositeDisposable = [];
    private static bool _closing;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsStore = Services.GetRequiredService<ISettingsStore>();

            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            Services.GetRequiredService<ConfirmationService>().SetOwner(mainWindow);

            mainWindow.Opened += (_, _) =>
            {
                var logger = Services.GetRequiredService<ILoggerService>();
                _ = logger.InfoAsync($"Window opened in {Program.StartupStopwatch.ElapsedMilliseconds} ms");

                var notificationService = Services.GetRequiredService<INotificationService>();
                _compositeDisposable.Add(logger.WhenErrorLogged
                    .GroupBy(e => e.Message)
                    .SelectMany(group => group.Throttle(TimeSpan.FromSeconds(2)))
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Subscribe(entry => notificationService.Show(entry.Message, NotificationSeverity.Error)));

                ApplyShowButtonText(settingsStore.Current.ShowButtonText);
                _compositeDisposable.Add(settingsStore.Observe()
                    .Select(s => s.ShowButtonText)
                    .DistinctUntilChanged()
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Subscribe(ApplyShowButtonText));

                ApplyTheme(settingsStore.Current.ApplicationTheme);
                _compositeDisposable.Add(settingsStore.Observe()
                    .Select(s => s.ApplicationTheme)
                    .DistinctUntilChanged()
                    .Subscribe(ApplyTheme));

                var trackStore = Services.GetRequiredService<ITrackStore>();

                // Before the music directory, deliberately. The declarations are what a scan reads
                // with, and setting them second would scan the whole library once under the old
                // rules and then immediately again under the new ones.
                _compositeDisposable.Add(settingsStore.Observe()
                    .Select(s => s.Discovery)
                    .DistinctUntilChanged()
                    .Subscribe(discovery => trackStore.DiscoverySettings = discovery));

                _compositeDisposable.Add(settingsStore.Observe()
                    .Select(s => s.AllowDancesOutsideTheList)
                    .DistinctUntilChanged()
                    .Subscribe(allow => trackStore.AllowDancesOutsideTheList = allow));

                _compositeDisposable.Add(settingsStore.Observe()
                    .Select(s => s.MusicDirectoryPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(r => new DirectoryInfo(r))
                    .Subscribe(directory => trackStore.MusicDirectory = directory));

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

                // The embedded server follows the settings live: switching it on, moving its port or
                // opening it to the network never needs a restart.
                var webServer = Services.GetRequiredService<PresentationWebServer>();
                ApplyWebServer(webServer, settingsStore.Current);
                _compositeDisposable.Add(settingsStore.Observe()
                    .Select(ToWebServerOptions)
                    .DistinctUntilChanged()
                    .Subscribe(options => webServer.ApplyAsync(options).SafeFireAndForget(ex =>
                        Services.GetRequiredService<ILoggerService>()
                            .ErrorAsync("Failed to apply presentation server settings", ex))));

                // Open initial presentation windows and subscribe to count changes
                SyncPresentationWindows(settingsStore.Current.PresentationDisplayCount, settingsStore);

                _compositeDisposable.Add(settingsStore.Observe()
                    .Select(s => s.PresentationDisplayCount)
                    .DistinctUntilChanged()
                    .Subscribe(count => SyncPresentationWindows(count, settingsStore)));
            };

            _compositeDisposable.Add(Observable.FromEventPattern(
                    h => mainWindow.Opened += h,
                    h => mainWindow.Opened -= h)
                .Take(1)
                .SelectMany(_ =>
                    Observable.Merge(
                        RunLoad<IDanceListStore>((s, token) => s.LoadAsync(token), "Failed to load the dance list"),
                        // Opened before anything asks it a question: the track store reads it on
                        // the first music directory it is handed, which can be immediately.
                        RunLoad<ILibraryIndex>((s, token) => s.OpenAsync(token), "Failed to open the library index"),
                        RunLoad<IQueueHistoryStore>((s, token) => s.LoadAsync(token), "Failed to load queue history")
                    // ToList waits for every load to finish before emitting once. The wizard reads
                    // the dance list to decide what to show, so it cannot open while that load is
                    // still in flight, or a profile that has a list looks like a fresh one.
                    ).ToList()
                )
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    ShowSetupIfNeeded(settingsStore);
                    RefreshDanceListInTheBackground();
                }));

            mainWindow.Closing += (_, e) =>
            {
                // The smoke test shuts the app down itself and there is nobody there to answer a
                // confirmation dialog, so the close has to go straight through.
                if (_closing || Program.IsSmokeTest)
                {
                    return;
                }

                // Cancel the close; HandleClosingAsync closes the window for real
                // (with _closing set) once the confirmation dialog and state saving
                // are done.
                e.Cancel = true;

                HandleClosingAsync(mainWindow, settingsStore).SafeFireAndForget(ex =>
                    Services.GetRequiredService<ILoggerService>()
                        .ErrorAsync("Failed to handle window closing", ex));
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task HandleClosingAsync(MainWindow mainWindow, ISettingsStore settingsStore)
    {
        var dialogVm = new ConfirmationDialogViewModel
        {
            Title = UiStrings.App_ExitTitle,
            Message = UiStrings.App_ExitMessage
        };
        var dialog = new ConfirmationDialogView
        {
            DataContext = dialogVm
        };
        await dialog.ShowDialog(mainWindow);

        if (dialogVm.DialogResult != true)
        {
            return;
        }

        // Save all window states while everything is still open
        var isMaximized = mainWindow.WindowState == AvaloniaWindowState.Maximized;
        var bounds = mainWindow.Bounds;
        var position = mainWindow.Position;

        var presentationStates = _presentationWindows.Select(w =>
        {
            var wBounds = w.Bounds;
            var wPosition = w.Position;
            return new DomainWindowState(
                wPosition.X,
                wPosition.Y,
                wBounds.Width,
                wBounds.Height,
                w.WindowState == AvaloniaWindowState.Maximized,
                w.IsBorderless);
        }).ToList();

        await settingsStore.UpdateAsync(s => s with
        {
            MainWindowState = new DomainWindowState(
                position.X,
                position.Y,
                bounds.Width,
                bounds.Height,
                isMaximized),
            PresentationWindowStates = presentationStates
        });

        // Asked to stop, never waited for. The process is about to end and the socket goes with
        // it, so there is nothing here worth making the user watch: awaiting Kestrel's drain is
        // what made the close button appear to do nothing while a browser held a WebSocket open.
        Services.GetRequiredService<PresentationWebServer>()
            .DisposeAsync()
            .AsTask()
            .SafeFireAndForget(ex => Services.GetRequiredService<ILoggerService>()
                .ErrorAsync("Presentation server did not shut down cleanly", ex));

        // Close presentation windows
        _compositeDisposable.Dispose();

        foreach (var pw in _presentationWindows)
        {
            pw.AllowClose = true;
            pw.Close();
        }

        _presentationWindows.Clear();

        // Now close for real — second Closing invocation will pass through

        _closing = true;
        mainWindow.Close();
    }

    /// <summary>Opens the setup wizard on a profile that has never been through it.</summary>
    /// <remarks>
    /// Not for the smoke test: it drives the application with nobody there to answer a wizard, and a
    /// modal window would simply hold it until CI gave up.
    /// </remarks>
    /// <summary>Sends a profile that has never been through setup to it.</summary>
    /// <remarks>
    /// Not for the smoke test: it drives the application with nobody there to answer a wizard, and
    /// it would simply sit on the first step until CI gave up.
    /// </remarks>
    internal static void ShowSetupIfNeeded(ISettingsStore settingsStore)
    {
        if (Program.IsSmokeTest || settingsStore.Current.SetupCompleted)
        {
            return;
        }

        ShowSetup();
    }

    internal static void ShowSetup() =>
        Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Setup;

    private static WebServerOptions ToWebServerOptions(ApplicationSettings settings) => new(
        settings.WebServerEnabled,
        settings.WebServerPortClamped,
        settings.WebRemoteControlEnabled,
        settings.WebRemoteControlPin);

    private static void ApplyWebServer(PresentationWebServer server, ApplicationSettings settings) =>
        server.ApplyAsync(ToWebServerOptions(settings)).SafeFireAndForget(ex =>
            Services.GetRequiredService<ILoggerService>()
                .ErrorAsync("Failed to start the presentation server", ex));

    /// <summary>
    /// Asks BigBalfolkList for a newer list, without anybody waiting for the answer. The window is
    /// already open on the list it has, and a hall with no wifi must cost nothing but a log line.
    /// </summary>
    private static void RefreshDanceListInTheBackground() =>
        Services.GetRequiredService<IDanceListStore>().RefreshAsync().SafeFireAndForget(exception =>
            Services.GetRequiredService<ILoggerService>()
                .ErrorAsync("Failed to refresh the dance list", exception));

    private static IObservable<Unit> RunLoad<T>(Func<T, CancellationToken, Task> loader, string errorMessage) where T : notnull
    {
        return Observable.Defer(() =>
        {
            var logger = Services.GetRequiredService<ILoggerService>();
            var service = Services.GetRequiredService<T>();

            return Observable.FromAsync(token => loader(service, token))
                .TimeInterval()
                .Do(ti => logger.InfoAsync($"{typeof(T).Name} completed | Duration: {ti.Interval:g}"))
                .Select(ti => ti.Value)
                .SubscribeOn(RxSchedulers.TaskpoolScheduler)
                .Catch<Unit, Exception>(ex =>
                {
                    logger.ErrorAsync(errorMessage, ex);
                    // Just continue
                    return Observable.Empty<Unit>();
                });
        });
    }

    // A resource rather than a property on each control: the App.axaml style pushes it into every
    // ButtonContent at once, so call sites only supply their icon and label.
    private void ApplyShowButtonText(bool showText) => Resources["ShowButtonText"] = showText;

    private void ApplyTheme(ApplicationTheme theme)
    {
        RequestedThemeVariant = theme switch
        {
            ApplicationTheme.Light => ThemeVariant.Light,
            ApplicationTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private void SyncPresentationWindows(int targetCount, ISettingsStore settingsStore)
    {
        targetCount = Math.Clamp(targetCount, 0, 10);

        // Close excess windows
        while (_presentationWindows.Count > targetCount)
        {
            var last = _presentationWindows[^1];
            _presentationWindows.RemoveAt(_presentationWindows.Count - 1);
            last.AllowClose = true;
            last.Close();
        }

        // Open new windows
        var savedStates = settingsStore.Current.PresentationWindowStates.ToList();
        while (_presentationWindows.Count < targetCount)
        {
            var index = _presentationWindows.Count;
            var window = new PresentationWindow
            {
                WindowIndex = index,
                Title = string.Format(CultureInfo.CurrentCulture, UiStrings.Presentation_WindowTitle, index + 1)
            };

            // Restore state after the window is shown so the WM respects position
            if (index < savedStates.Count)
            {
                var ws = savedStates[index];
                window.Opened += (_, _) =>
                {
                    if (ws is { X: not null, Y: not null })
                    {
                        window.Position = new PixelPoint((int)ws.X.Value, (int)ws.Y.Value);
                    }

                    if (ws is { Width: not null, Height: not null })
                    {
                        window.Width = ws.Width.Value;
                        window.Height = ws.Height.Value;
                    }

                    if (ws.IsBorderless)
                    {
                        window.IsBorderless = true;
                    }
                    else if (ws.IsMaximized)
                    {
                        window.WindowState = AvaloniaWindowState.Maximized;
                    }
                };
            }

            window.Show();
            _presentationWindows.Add(window);
        }
    }
}
