using System;
using System.Diagnostics;
using System.Globalization;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Logging;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Avalonia.Reactive.Splat;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.UI.Views.Equalizer;
using Ready4Balfolk.UI.Views.Help;
using Ready4Balfolk.UI.Views.History;
using Ready4Balfolk.UI.Views.Playback;
using Ready4Balfolk.UI.Views.Presentation;
using Ready4Balfolk.UI.Views.Queue;
using Ready4Balfolk.UI.Views.Settings;
using Ready4Balfolk.UI.Views.Toolbar;
using Ready4Balfolk.UI.Views.TrackCatalog;
using Ready4Balfolk.UI.Views.Wizard;
using Ready4Balfolk.Web;
using FileLogSinkService = Ready4Balfolk.UI.Services.FileLogSinkService;

namespace Ready4Balfolk.UI;

public static class Program
{
    internal static readonly Stopwatch StartupStopwatch = new();

    /// <summary>
    /// True when started with <c>--smoke-test</c>: the app comes up for a CI check rather than for
    /// a user, so it must be able to shut itself down and must not depend on an audio device.
    /// </summary>
    internal static bool IsSmokeTest { get; private set; }

    private static LogLevel _minimumLogLevel = LogLevel.Info;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        StartupStopwatch.Start();

        // ReSharper disable once RedundantAssignment
        var isDebug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);
#if DEBUG
        isDebug = true;
#endif
        if (isDebug)
        {
            _minimumLogLevel = LogLevel.Debug;
        }

        IsSmokeTest = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);

        try
        {
            return IsSmokeTest
                ? SmokeTest.Run(BuildAvaloniaApp(), args)
                : BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            _ = App.Services?.GetService<ILoggerService>()?.CriticalAsync("Fatal startup exception", ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Takes over from the X11 backend selected above whenever a Wayland compositor is
            // present, and leaves it in place otherwise. Must follow UsePlatformDetect.
            .UseWaylandWithFallback()
            // The X11 backend exports a global menu over DBus to com.canonical.AppMenu.Registrar,
            // which only exists under Unity-style panels. The app has a toolbar and no native menu
            // bar, so the export buys nothing and throws on every other desktop.
            .With(new X11PlatformOptions
            {
                UseDBusMenu = false,
                // Desktops match a window to its launcher entry by comparing WM_CLASS to the
                // desktop file basename, and without that the taskbar shows a window with no
                // icon next to a launcher that has one. Wayland sessions still miss out: that
                // backend never sets the equivalent app_id at all, which is issue #46.
                WmClass = "io.github.tjvl.Ready4Balfolk"
            })
            .UseReactiveUIWithMicrosoftDependencyResolver(
                ConfigureServices,
                withResolver: sp => App.Services = sp!,
                // Replaces the RxApp.DefaultExceptionHandler assignment removed in ReactiveUI 23.
                // Resolved lazily: the logger service does not exist yet when the builder runs.
                withReactiveUIBuilder: builder => builder.WithExceptionHandler(Observer.Create<Exception>(ex =>
                    _ = App.Services?.GetService<ILoggerService>()?.ErrorAsync("Unhandled RxApp exception", ex))))
            .WithInterFont()
            .AfterSetup(_ =>
            {
                var settingsStore = App.Services.GetRequiredService<ISettingsStore>();
                var loggerService = App.Services.GetRequiredService<ILoggerService>();

                Logger.Sink = new FileLogSinkService(loggerService);
                var culture = settingsStore.Current.ApplicationLanguage switch
                {
                    ApplicationLanguage.Dutch => new CultureInfo("nl"),
                    _ => new CultureInfo("en")
                };
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    if (e.ExceptionObject is Exception ex)
                    {
                        loggerService.CriticalAsync("Unhandled exception", ex);
                    }
                };

                TaskScheduler.UnobservedTaskException += (_, e) =>
                {
                    loggerService.ErrorAsync("Unobserved task exception", e.Exception);
                    e.SetObserved();
                };

                loggerService.InfoAsync("Application starting");
            });

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<ILoggerService>(sp =>
            new FileLoggerService(sp.GetRequiredService<IApplicationSettingsDirectory>().DirectoryInfoRoot)
            {
                MinimumLevel = _minimumLogLevel
            });
        services.AddSingleton<IAudioPlaybackService>(sp => new ManagedBassAudioPlaybackService(
            sp.GetRequiredService<ILoggerService>(),
            sp.GetRequiredService<ISettingsStore>(),
            useNoSoundDevice: IsSmokeTest));
        services.AddSingleton<IQueueService>(sp =>
        {
            var consumption =
                new Lazy<IQueueConsumptionService>(sp.GetRequiredService<IQueueConsumptionService>);
            return new QueueService(
                sp.GetRequiredService<ISettingsStore>(),
                sp.GetRequiredService<IQueueHistoryStore>(),
                () => consumption.Value.CurrentItem,
                () => consumption.Value.CurrentItemRemaining,
                sp.GetRequiredService<ILoggerService>());
        });
        services.AddSingleton<IQueueConsumptionService, QueueConsumptionService>();
        services.AddSingleton<IApplicationSettingsDirectory, ApplicationSettingsDirectory>();
        services.AddTransient<IEditorHistoryService, EditorHistoryService>();
        services.AddSingleton<ITrackDurationCache, TrackDurationCache>();
        services.AddSingleton<ITrackDiscoveryService, TrackDiscoveryService>();
        services.AddSingleton<IRandomTrackService, RandomTrackService>();
        services.AddSingleton<IPresentationStateService, PresentationStateService>();

        // Stores
        services.AddSingleton<IDanceListStore, DanceListStore>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<IQueueHistoryStore, QueueHistoryStore>();
        services.AddSingleton<ITrackStore, TrackStore>();

        // UI layer services
        services.AddSingleton<NavigationService>();
        // Forward to the concrete registration rather than registering the implementation again:
        // AddSingleton<TService, TImplementation> would build a second instance, and both of these
        // carry state set from outside (the dialog owner, the notification list the overlay binds
        // to) that would then be written on one instance and read from the other.
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<ConfirmationService>();
        services.AddSingleton<IConfirmationService>(sp => sp.GetRequiredService<ConfirmationService>());

        // The embedded server. It builds its own container and is handed these same instances by
        // AddForwardedHostServices, so nothing here is ever constructed twice.
        services.AddSingleton<IRemoteCommandDispatcher, AvaloniaRemoteCommandDispatcher>();
        services.AddSingleton(sp => new PresentationWebServer(sp, sp.GetRequiredService<ILoggerService>()));

        // ViewModels
        services.AddSingleton<ToolbarViewModel>();
        services.AddSingleton<PlaybackViewModel>();
        services.AddSingleton<EqualizerViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<TrackCatalogViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<HelpViewModel>();
        services.AddSingleton<DanceListViewModel>();
        services.AddSingleton<Lazy<DanceListViewModel>>(sp => new(sp.GetRequiredService<DanceListViewModel>));

        // Setup wizard. Transient: it is built when the wizard opens and thrown away when it
        // closes, so a second run starts from what is on disk rather than from the last visit.
        services.AddTransient<DanceListStepViewModel>();
        services.AddTransient<DanceListEditStepViewModel>();
        services.AddTransient<MusicDirectoryStepViewModel>();
        services.AddTransient<SetupWizardViewModel>();
        services.AddTransient<IViewFor<DanceListStepViewModel>, DanceListStepView>();
        services.AddTransient<IViewFor<DanceListEditStepViewModel>, DanceListEditStepView>();
        services.AddTransient<IViewFor<MusicDirectoryStepViewModel>, MusicDirectoryStepView>();

        // Lazy wrappers — defers ViewModel creation until first navigation/toggle
        services.AddSingleton<Lazy<HistoryViewModel>>(sp => new(sp.GetRequiredService<HistoryViewModel>));
        services.AddSingleton<Lazy<SettingsViewModel>>(sp => new(sp.GetRequiredService<SettingsViewModel>));
        services.AddSingleton<Lazy<HelpViewModel>>(sp => new(sp.GetRequiredService<HelpViewModel>));
        // View registrations for ViewModelViewHost resolution
        services.AddTransient<IViewFor<SettingsViewModel>, SettingsView>();
        services.AddTransient<IViewFor<HelpViewModel>, HelpView>();

        services.AddSingleton<PresentationDisplayViewModel>();

        // MainWindowViewModel
        services.AddSingleton<MainWindowViewModel>();
    }
}
