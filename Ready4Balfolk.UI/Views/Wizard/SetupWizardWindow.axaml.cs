using System;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Wizard;

public partial class SetupWizardWindow : ReactiveWindow<SetupWizardViewModel>
{
    private IDisposable? _confirmationOwnership;

    public SetupWizardWindow()
    {
        InitializeComponent();

        // Confirmations raised from a wizard step belong to the wizard, not to the main window it
        // is modal over.
        Opened += (_, _) => _confirmationOwnership =
            App.Services.GetRequiredService<ConfirmationService>().UseOwner(this);

        Closed += (_, _) =>
        {
            _confirmationOwnership?.Dispose();
            _confirmationOwnership = null;
        };

        this.WhenActivated(d => d(ViewModel!.Finished.Subscribe(_ => Close())));
    }
}
