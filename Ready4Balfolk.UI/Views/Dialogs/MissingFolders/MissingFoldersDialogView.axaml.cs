using System;
using System.Reactive.Linq;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;
using Ready4Balfolk.UI.Platform;

namespace Ready4Balfolk.UI.Views.Dialogs.MissingFolders;

public partial class MissingFoldersDialogView : ReactiveWindow<MissingFoldersDialogViewModel>
{
    public MissingFoldersDialogView()
    {
        InitializeComponent();

        // Before the window is shown, so the compositor already knows the app id when the
        // surface is mapped. See WaylandAppId.
        WaylandAppId.Apply(this);

        Opened += (_, _) => KeepThemButton.Focus();

        this.WhenActivated(d => d(this.WhenAnyValue(x => x.ViewModel!.Answer)
            .Where(answer => answer.HasValue)
            .Subscribe(_ => Close())));
    }
}
