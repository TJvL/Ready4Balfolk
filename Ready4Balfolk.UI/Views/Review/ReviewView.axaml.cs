using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI.Avalonia.Reactive;

namespace Ready4Balfolk.UI.Views.Review;

/// <summary>
/// The review queue, driven from the keyboard.
/// </summary>
/// <remarks>
/// Two thousand mouse trips is the difference between an evening and never, so answering a row and
/// answering a folder are both one keystroke. The shortcut for a folder is deliberately not live
/// while a field is being typed into, or writing "Mazurka" would approve a folder halfway through
/// the word.
/// </remarks>
public partial class ReviewView : ReactiveUserControl<ReviewViewModel>
{
    public ReviewView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { Selected: { } selected })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                ViewModel.ApproveCommand.Execute(selected).Subscribe();
                e.Handled = true;
                break;

            case Key.A when !IsTyping():
                ViewModel.ApproveFolderCommand.Execute(selected).Subscribe();
                e.Handled = true;
                break;

            default:
                break;
        }
    }

    private async void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ReviewRowViewModel row } && ViewModel is { } viewModel)
        {
            await viewModel.TogglePreviewAsync(row);
        }
    }

    /// <summary>Seeking by clicking the bar, which is what makes skimming a track possible.</summary>
    private void OnPreviewBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ProgressBar bar || ViewModel is not { PreviewDurationSeconds: > 0 } viewModel)
        {
            return;
        }

        var ratio = Math.Clamp(e.GetPosition(bar).X / bar.Bounds.Width, 0, 1);
        _ = viewModel.SeekPreviewAsync(TimeSpan.FromSeconds(ratio * viewModel.PreviewDurationSeconds));
    }

    /// <summary>True while the focus sits in something a letter belongs in.</summary>
    private bool IsTyping() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox or AutoCompleteBox;
}
