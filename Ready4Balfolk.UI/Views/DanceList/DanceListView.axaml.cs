using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceList;

public partial class DanceListView : ReactiveUserControl<DanceListViewModel>
{
    /// <summary>Whatever in this panel last had the keyboard, while it still has it.</summary>
    private Control? _standingOn;

    public DanceListView()
    {
        InitializeComponent();

        // Everything in the panel that takes the keyboard, rather than the two or three a name
        // could list: what this is watching for is a control going away under the DJ, and that is
        // the same event whichever of them it was.
        AddHandler(GotFocusEvent, WhateverTookTheKeyboard, RoutingStrategies.Bubble);
    }

    /// <summary>
    /// The offline path to a newer list: a <c>dances.json</c> carried in on a stick, for a machine
    /// that never reaches the internet. It goes through the same reader a download does.
    /// </summary>
    private void OnUpdateFromFileClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run(UiStrings.DanceList_UpdateFromFileFailed, async () =>
        {
            var path = await App.Services.GetRequiredService<IFilePickerService>()
                .PickFileToOpenAsync(UiStrings.DanceList_UpdateFromFileTip, FileKind.Json);

            if (path is not null)
            {
                // Every failure is reported by the view model as a notification, because a refused
                // file is an ordinary answer here rather than an exception the user can act on.
                await ViewModel!.UpdateFromFileAsync(path);
            }
        });

    /// <summary>Remembers what the keyboard is on, so the panel can give it back.</summary>
    private void WhateverTookTheKeyboard(object? sender, FocusChangedEventArgs e)
    {
        Forget();

        // Only the things drawn one per card or one per chip. The search box and the buttons down
        // the side are drawn once and stay, so nothing ever takes them away from under the DJ.
        if (e.Source is Control control && control.DataContext is IKeepsItsPlace)
        {
            _standingOn = control;
            control.DetachedFromVisualTree += WhatHadTheKeyboardWentAway;
        }
    }

    /// <summary>
    /// Puts the keyboard back on a card or a chip the panel had to draw again somewhere else.
    /// </summary>
    /// <remarks>
    /// The panel keeps its cards and its chips rather than replacing them, but a card that has to
    /// be shown further up or further down is still a control built fresh where it lands: Avalonia
    /// builds a new container for an item that moves, and the old one, keyboard and all, is thrown
    /// away. Focus then sits nowhere, so the next space runs the transport instead of the dice the
    /// DJ was standing on. What survives a move is the item, so the item is what the keyboard is
    /// found again by.
    /// </remarks>
    private void WhatHadTheKeyboardWentAway(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_standingOn is not { } gone)
        {
            return;
        }

        var key = (gone.DataContext as IKeepsItsPlace)?.Key;
        var part = AutomationProperties.GetAutomationId(gone) ?? string.Empty;
        Forget();

        if (key is null)
        {
            return;
        }

        // Once the panel has drawn wherever the item went: the container it is being given is
        // built in this same pass, and focusing something not yet there does nothing at all.
        Dispatcher.UIThread.Post(() => PutTheKeyboardBackOn(key, part), DispatcherPriority.Loaded);
    }

    private void PutTheKeyboardBackOn(string key, string part)
    {
        // Only where it went nowhere at all. A control detached while the keyboard is on it leaves
        // the window with nothing focused; if anything has taken the keyboard since, it is theirs.
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not null)
        {
            return;
        }

        var again = this.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control =>
                control.DataContext is IKeepsItsPlace keeps
                && string.Equals(keeps.Key, key, StringComparison.Ordinal)
                && string.Equals(AutomationProperties.GetAutomationId(control) ?? string.Empty, part, StringComparison.Ordinal)
                && control.Focusable
                && control.IsEffectivelyVisible);

        if (again is null)
        {
            // The card or the chip is genuinely gone, which is an ordinary thing for a newer list
            // to do. There is nothing to stand on, and the keyboard is left where Tab can pick it
            // up again rather than being put somewhere arbitrary.
            return;
        }

        again.BringIntoView();
        again.Focus();
    }

    private void Forget()
    {
        if (_standingOn is { } was)
        {
            was.DetachedFromVisualTree -= WhatHadTheKeyboardWentAway;
            _standingOn = null;
        }
    }
}
