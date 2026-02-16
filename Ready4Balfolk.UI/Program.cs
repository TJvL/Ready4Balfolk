using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Logging;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Avalonia.Splat;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Synonym;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Synonym;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Domain.Stores.Tree;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceSynonyms;
using Ready4Balfolk.UI.Views.DanceTree;
using Ready4Balfolk.UI.Views.Help;
using Ready4Balfolk.UI.Views.History;
using Ready4Balfolk.UI.Views.Playback;
using Ready4Balfolk.UI.Views.Presentation;
using Ready4Balfolk.UI.Views.Queue;
using Ready4Balfolk.UI.Views.Settings;
using Ready4Balfolk.UI.Views.Toolbar;
using Ready4Balfolk.UI.Views.TrackCatalog;
using FileLogSinkService = Ready4Balfolk.UI.Services.FileLogSinkService;

namespace Ready4Balfolk.UI;

public static class Program
{
    internal static readonly Stopwatch StartupStopwatch = new();

    private static readonly DirectoryInfo DataDirectory =
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ready4Balfolk"));

    private static readonly FileLoggerService LoggerService = new(DataDirectory);
    private static readonly SettingsStore SettingsStore = new(DataDirectory);

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        StartupStopwatch.Start();

        // ReSharper disable once RedundantAssignment
        var isDebug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);
#if DEBUG
        isDebug = true;
#endif
        if (isDebug)
        {
            LoggerService.MinimumLevel = LogLevel.Debug;
        }

        var culture = SettingsStore.Current.ApplicationLanguage switch
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
                LoggerService.CriticalAsync("Unhandled exception", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LoggerService.ErrorAsync("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex =>
            LoggerService.ErrorAsync("Unhandled RxApp exception", ex));

        LoggerService.InfoAsync("Application starting");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LoggerService.CriticalAsync("Fatal startup exception", ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUIWithMicrosoftDependencyResolver(
                ConfigureServices,
                withResolver: sp => App.Services = sp!)
            .WithInterFont()
            .AfterSetup(_ => Logger.Sink = new FileLogSinkService(LoggerService));

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<ILoggerService>(LoggerService);
        services.AddSingleton<IAudioPlaybackService, ManagedBassAudioPlaybackService>();
        services.AddSingleton<IQueueService>(sp =>
        {
            var consumption =
                new Lazy<IQueueConsumptionService>(sp.GetRequiredService<IQueueConsumptionService>);
            return new QueueService(
                sp.GetRequiredService<ISettingsStore>(),
                sp.GetRequiredService<IQueueHistoryStore>(),
                () => consumption.Value.CurrentItem);
        });
        services.AddSingleton<IQueueConsumptionService, QueueConsumptionService>();
        services.AddTransient<IEditorHistoryService, EditorHistoryService>();
        services.AddSingleton<ITrackDurationCache>(_ => new TrackDurationCache(DataDirectory));
        services.AddSingleton<ITrackDiscoveryService, TrackDiscoveryService>();
        services.AddSingleton<IRandomTrackService, RandomTrackService>();
        services.AddSingleton<ISynonymResolutionService, SynonymResolutionService>();

        // Stores
        services.AddSingleton<IDanceTreeStore>(_ => new DanceTreeStore(DataDirectory));
        services.AddSingleton<IDanceSynonymStore>(_ => new DanceSynonymStore(DataDirectory));
        services.AddSingleton<ISettingsStore>(SettingsStore);
        services.AddSingleton<IQueueHistoryStore>(_ => new QueueHistoryStore(DataDirectory));
        services.AddSingleton<ITrackStore, TrackStore>();

        // UI layer services
        services.AddSingleton<NavigationService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<ConfirmationService>();
        services.AddSingleton<IConfirmationService>(sp => sp.GetRequiredService<ConfirmationService>());

        // ViewModels
        services.AddSingleton<ToolbarViewModel>();
        services.AddSingleton<PlaybackViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<TrackCatalogViewModel>();
        services.AddSingleton<DanceTreeViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<HelpViewModel>();
        services.AddSingleton<DanceSynonymsViewModel>();
        services.AddSingleton<PresentationDisplayViewModel>();

        // MainWindowViewModel
        services.AddSingleton<MainWindowViewModel>();
    }
}
