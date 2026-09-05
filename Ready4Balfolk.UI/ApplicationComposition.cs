using System;
using System.Globalization;
using System.IO.Abstractions;
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
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.UI.Views.Discovery;
using Ready4Balfolk.UI.Views.Equalizer;
using Ready4Balfolk.UI.Views.Help;
using Ready4Balfolk.UI.Views.History;
using Ready4Balfolk.UI.Views.Playback;
using Ready4Balfolk.UI.Views.Presentation;
using Ready4Balfolk.UI.Views.Queue;
using Ready4Balfolk.UI.Views.Review;
using Ready4Balfolk.UI.Views.Settings;
using Ready4Balfolk.UI.Views.Toolbar;
using Ready4Balfolk.UI.Views.TrackCatalog;
using Ready4Balfolk.UI.Views.Wizard;
using Ready4Balfolk.Web;
using FileLogSinkService = Ready4Balfolk.UI.Services.FileLogSinkService;

namespace Ready4Balfolk.UI;

/// <summary>Everything the application is, apart from the platform it is drawn on.</summary>
/// <remarks>
/// <see cref="Program"/> picks a windowing backend and hands the builder here; a scenario run picks
/// the headless one and hands it to the same place. What is registered, and everything the app does
/// once the framework is up, is therefore one description rather than two that have to be kept in
/// step.
/// </remarks>
public static class ApplicationComposition
{
    /// <summary>Adds everything that is not the windowing backend to a builder.</summary>
    public static AppBuilder Configure(AppBuilder builder, ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder
            .UseReactiveUIWithMicrosoftDependencyResolver(
                services => ConfigureServices(services, options),
                withResolver: sp => App.UseServices(sp!),
                // Replaces the RxApp.DefaultExceptionHandler assignment removed in ReactiveUI 23.
                // Resolved lazily: the logger service does not exist yet when the builder runs.
                withReactiveUIBuilder: reactiveUi => reactiveUi.WithExceptionHandler(
                    Observer.Create<Exception>(ReportUnhandled)))
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
    }

    /// <summary>Writes down what fell out of a subscription, and never throws doing it.</summary>
    /// <remarks>
    /// The container it asks for the logger can be gone: on the way down, anything still in flight
    /// arrives after everything it needs has been disposed. An exception handler that throws while
    /// reporting an exception replaces a line in the log with a crash.
    /// </remarks>
    private static void ReportUnhandled(Exception exception)
    {
        try
        {
            _ = App.Services?.GetService<ILoggerService>()?.ErrorAsync("Unhandled RxApp exception", exception);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void ConfigureServices(IServiceCollection services, ApplicationOptions options)
    {
        // Services
        // The clock, everywhere it decides something: the cutoff and its grace, when an item
        // started, how long a delay has run, when a night began and ended, whether an evening was
        // left unfinished, and how long a remote's token is good for.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILoggerService>(sp =>
            new FileLoggerService(sp.GetRequiredService<IApplicationSettingsDirectory>().DirectoryInfoRoot)
            {
                MinimumLevel = options.MinimumLogLevel
            });
        services.AddSingleton<IAudioPlaybackService>(sp => new ManagedBassAudioPlaybackService(
            sp.GetRequiredService<ILoggerService>(),
            sp.GetRequiredService<ISettingsStore>(),
            useNoSoundDevice: options.UseNoSoundDevice));
        services.AddSingleton<IQueueService>(sp =>
        {
            var consumption =
                new Lazy<IQueueConsumptionService>(sp.GetRequiredService<IQueueConsumptionService>);
            return new QueueService(
                sp.GetRequiredService<ISettingsStore>(),
                sp.GetRequiredService<IQueueHistoryStore>(),
                sp.GetRequiredService<ITrackStore>(),
                () => consumption.Value.CurrentItem,
                () => consumption.Value.CurrentItemRemaining,
                sp.GetRequiredService<ILoggerService>(),
                sp.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton<IQueueConsumptionService, QueueConsumptionService>();
        services.AddSingleton<IEndOfNightAudio, EndOfNightAudio>();
        // Registered from the options when a run brought its own, which is how a scenario gets a
        // data directory of its own without the stores hanging off it being anything but real.
        if (options.SettingsDirectory is { } settingsDirectory)
        {
            services.AddSingleton(settingsDirectory);
        }
        else
        {
            services.AddSingleton<IApplicationSettingsDirectory, ApplicationSettingsDirectory>();
        }
        services.AddSingleton<ILibraryIndex, SqliteLibraryIndex>();
        services.AddSingleton<IFileSystem>(new FileSystem());
        services.AddSingleton<TrackEditorService>();
        services.AddSingleton<ITrackDiscoveryService, TrackDiscoveryService>();
        services.AddSingleton<IRandomTrackService, RandomTrackService>();
        services.AddSingleton<IPresentationStateService, PresentationStateService>();
        services.AddSingleton<IPreviewPlaybackService, PreviewPlaybackService>();

        // Stores
        services.AddSingleton<IDanceListStore, DanceListStore>();
        services.AddSingleton<IDanceListFeed, DanceListFeed>();
        services.AddSingleton<IDancePool, DancePool>();
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
        services.AddSingleton<FilePickerService>();
        services.AddSingleton<IFilePickerService>(sp => sp.GetRequiredService<FilePickerService>());
        services.AddSingleton<ConfirmationService>();
        services.AddSingleton<IConfirmationService>(sp => sp.GetRequiredService<ConfirmationService>());
        services.AddSingleton<MissingFolderPromptService>();
        services.AddSingleton<IMissingFolderPrompt>(sp => sp.GetRequiredService<MissingFolderPromptService>());

        // The embedded server. It builds its own container and is handed these same instances by
        // AddForwardedHostServices, so nothing here is ever constructed twice.
        services.AddSingleton<IRemoteCommandDispatcher, AvaloniaRemoteCommandDispatcher>();
        services.AddSingleton(sp => new PresentationWebServer(
            sp, sp.GetRequiredService<ILoggerService>(), sp.GetRequiredService<TimeProvider>()));

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
        services.AddSingleton<DiscoveryViewModel>();
        services.AddSingleton<ReviewViewModel>();
        services.AddSingleton<Lazy<DanceListViewModel>>(sp => new(sp.GetRequiredService<DanceListViewModel>));

        // Setup wizard. Transient: it is built when the wizard opens and thrown away when it
        // closes, so a second run starts from what is on disk rather than from the last visit.
        services.AddTransient<WelcomeStepViewModel>();
        services.AddTransient<DanceListStepViewModel>();
        services.AddTransient<DiscoveryStepViewModel>();
        services.AddTransient<ReviewStepViewModel>();
        services.AddTransient<MusicDirectoryStepViewModel>();
        services.AddTransient<SetupWizardViewModel>();
        services.AddSingleton<Func<SetupWizardViewModel>>(sp => sp.GetRequiredService<SetupWizardViewModel>);
        services.AddTransient<IViewFor<SetupWizardViewModel>, SetupWizardView>();
        services.AddTransient<IViewFor<WelcomeStepViewModel>, WelcomeStepView>();
        services.AddTransient<IViewFor<DanceListStepViewModel>, DanceListStepView>();
        services.AddTransient<IViewFor<DiscoveryStepViewModel>, DiscoveryStepView>();
        services.AddTransient<IViewFor<ReviewStepViewModel>, ReviewStepView>();
        services.AddTransient<IViewFor<MusicDirectoryStepViewModel>, MusicDirectoryStepView>();

        // Lazy wrappers: defers ViewModel creation until first navigation/toggle
        services.AddSingleton<Lazy<HistoryViewModel>>(sp => new(sp.GetRequiredService<HistoryViewModel>));
        services.AddSingleton<Lazy<SettingsViewModel>>(sp => new(sp.GetRequiredService<SettingsViewModel>));
        services.AddSingleton<Lazy<HelpViewModel>>(sp => new(sp.GetRequiredService<HelpViewModel>));
        services.AddSingleton<Lazy<ReviewViewModel>>(sp => new(sp.GetRequiredService<ReviewViewModel>));
        // View registrations for ViewModelViewHost resolution
        services.AddTransient<IViewFor<SettingsViewModel>, SettingsView>();
        services.AddTransient<IViewFor<HelpViewModel>, HelpView>();
        services.AddTransient<IViewFor<DiscoveryViewModel>, DiscoveryView>();
        services.AddTransient<IViewFor<ReviewViewModel>, ReviewView>();

        services.AddSingleton<PresentationDisplayViewModel>();

        // MainWindowViewModel
        services.AddSingleton<MainWindowViewModel>();

        // Startup orchestration, which used to be the body of App.OnFrameworkInitializationCompleted.
        services.AddSingleton<ApplicationStartup>();

        options.AlsoRegister?.Invoke(services);
    }
}
