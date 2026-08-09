using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI.Avalonia.Reactive;

namespace Ready4Balfolk.UI.Views.Tagging;

public partial class TaggingView : ReactiveUserControl<TaggingViewModel>
{
    public TaggingView()
    {
        InitializeComponent();

        PreviewProgressBar.PointerPressed += OnProgressBarPointerPressed;
    }

    private void OnProgressBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { PreviewDurationSeconds: > 0 } viewModel)
        {
            return;
        }

        var ratio = Math.Clamp(
            e.GetPosition(PreviewProgressBar).X / PreviewProgressBar.Bounds.Width, 0, 1);
        _ = viewModel.SeekPreviewAsync(TimeSpan.FromSeconds(ratio * viewModel.PreviewDurationSeconds));
    }

    private async void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: PreviewRowViewModel row })
        {
            await ViewModel!.TogglePreviewAsync(row);
        }
    }

    private async void OnIgnoreClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: UnrecognisedValueViewModel value })
        {
            await ViewModel!.ToggleIgnoreAsync(value);
        }
    }

    private async void OnMapSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is AutoCompleteBox { Tag: UnrecognisedValueViewModel value, SelectedItem: DanceSuggestionRow row } box)
        {
            // Cleared afterwards: the box is a way of issuing a decision, and the decision itself is
            // shown on the row, which stays put so it can be corrected.
            await ViewModel!.MapAsync(value, row);
            Clear(box);
        }
    }

    private async void OnFolderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is AutoCompleteBox { Tag: FolderGroupViewModel folder, SelectedItem: DanceSuggestionRow row } box)
        {
            await ViewModel!.MapFolderAsync(folder, row);
            Clear(box);
        }
    }

    private async void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is AutoCompleteBox { Tag: PreviewRowViewModel track, SelectedItem: DanceSuggestionRow row } box)
        {
            await ViewModel!.MapTrackAsync(track, row);
            Clear(box);
        }
    }

    private static void Clear(AutoCompleteBox box) => Dispatcher.UIThread.Post(() =>
    {
        box.SelectedItem = null;
        box.Text = string.Empty;
    }, DispatcherPriority.Background);

}
