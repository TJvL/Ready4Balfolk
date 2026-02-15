using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Synonym;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Domain.Stores.Tree;
using Ready4Balfolk.UI.Views.DanceTree;
using Ready4Balfolk.UI.Views.Dialogs.Confirmation;
using Ready4Balfolk.UI.Views.Presentation;
using AvaloniaWindowState = Avalonia.Controls.WindowState;
using DomainWindowState = Ready4Balfolk.Domain.Models.Settings.WindowState;

namespace Ready4Balfolk.UI;

public sealed class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    private readonly List<PresentationWindow> _presentationWindows = [];
    private IDisposable? _presentationCountSubscription;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsStore = Services.GetRequiredService<ISettingsStore>();
            var closeConfirmed = false;

            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            Services.GetRequiredService<Services.ConfirmationService>().SetOwner(mainWindow);

            mainWindow.Opened += async (_, _) =>
            {
                await settingsStore.LoadAsync();
                await Services.GetRequiredService<IDanceTreeStore>().LoadAsync();
                await Services.GetRequiredService<IDanceSynonymStore>().LoadAsync();
                await Services.GetRequiredService<IQueueHistoryStore>().LoadAsync();

                ApplyTheme(settingsStore.Current.ApplicationTheme);
                settingsStore.Observe()
                    .Select(s => s.ApplicationTheme)
                    .DistinctUntilChanged()
                    .Subscribe(ApplyTheme);

                var trackStore = Services.GetRequiredService<ITrackStore>();
                settingsStore.Observe()
                    .Select(s => s.MusicDirectoryPath)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Subscribe(path => trackStore.MusicDirectory = new DirectoryInfo(path));

                var windowState = settingsStore.Current.MainWindowState;
                if (windowState is { X: not null, Y: not null })
                    mainWindow.Position = new PixelPoint((int)windowState.X.Value, (int)windowState.Y.Value);
                if (windowState is { Width: not null, Height: not null })
                {
                    mainWindow.Width = windowState.Width.Value;
                    mainWindow.Height = windowState.Height.Value;
                }

                if (windowState.IsMaximized)
                    mainWindow.WindowState = AvaloniaWindowState.Maximized;

                // Open initial presentation windows and subscribe to count changes
                SyncPresentationWindows(settingsStore.Current.PresentationDisplayCount, settingsStore);

                _presentationCountSubscription = settingsStore.Observe()
                    .Select(s => s.PresentationDisplayCount)
                    .DistinctUntilChanged()
                    .Subscribe(count => SyncPresentationWindows(count, settingsStore));
            };

            mainWindow.Closing += async (_, e) =>
            {
                if (closeConfirmed)
                    return;

                e.Cancel = true;

                var dialogVm = new ConfirmationDialogViewModel
                {
                    Title = "Exit",
                    Message = "Are you sure you want to quit?"
                };
                var dialog = new ConfirmationDialogView { DataContext = dialogVm };
                await dialog.ShowDialog(mainWindow);

                if (dialogVm.DialogResult != true)
                    return;

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

                var collapsedBranches = Services.GetRequiredService<DanceTreeViewModel>()
                    .GetCollapsedBranches();

                await settingsStore.UpdateAsync(s => s with
                {
                    MainWindowState = new DomainWindowState(
                        position.X,
                        position.Y,
                        bounds.Width,
                        bounds.Height,
                        isMaximized),
                    PresentationWindowStates = presentationStates,
                    CollapsedBranches = collapsedBranches
                });

                // Close presentation windows
                _presentationCountSubscription?.Dispose();
                foreach (var pw in _presentationWindows)
                {
                    pw.AllowClose = true;
                    pw.Close();
                }

                _presentationWindows.Clear();

                // Now close for real — second Closing invocation will pass through
                closeConfirmed = true;
                mainWindow.Close();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

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
                Title = $"Presentation Display {index + 1}"
            };

            // Restore state after the window is shown so the WM respects position
            if (index < savedStates.Count)
            {
                var ws = savedStates[index];
                window.Opened += (_, _) =>
                {
                    if (ws is { X: not null, Y: not null })
                        window.Position = new PixelPoint((int)ws.X.Value, (int)ws.Y.Value);
                    if (ws is { Width: not null, Height: not null })
                    {
                        window.Width = ws.Width.Value;
                        window.Height = ws.Height.Value;
                    }

                    if (ws.IsBorderless)
                        window.IsBorderless = true;
                    else if (ws.IsMaximized)
                        window.WindowState = AvaloniaWindowState.Maximized;
                };
            }

            window.Show();
            _presentationWindows.Add(window);
        }
    }
}
