using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.History;

public partial class HistoryView : ReactiveUserControl<HistoryViewModel>
{
    public HistoryView()
    {
        InitializeComponent();

        // History grows downwards, so the newest entry is the interesting one. The view is created
        // once and shown by toggling IsVisible, so this hooks visibility rather than activation:
        // WhenActivated would only ever fire on the first attach to the visual tree.
        this.GetObservable(IsVisibleProperty)
            .Where(visible => visible)
            .Subscribe(_ =>
            {
                // The nights on file are read when somebody looks at them, rather than on every
                // track that finishes: the list only changes when a night is opened or filed.
                Handlers.Run(
                    UiStrings.History_ReadNightsFailed,
                    () => ViewModel?.RefreshNightsAsync() ?? Task.CompletedTask);
                ScrollToLatest();
            });
    }

    private void ScrollToLatest() =>
        // Posted at Background priority so the list has realised and measured its items before we
        // ask it to scroll; scrolling in the same pass lands on a list that is still empty.
        Dispatcher.UIThread.Post(() =>
        {
            var lastIndex = HistoryList.ItemCount - 1;
            if (lastIndex >= 0)
            {
                HistoryList.ScrollIntoView(lastIndex);
            }
        }, DispatcherPriority.Background);
}
