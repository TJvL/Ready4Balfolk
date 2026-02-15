using System;
using System.Globalization;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Dialogs.Message;

public class RequestMessageDialogViewModel : ReactiveObject
{
    public string Message
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool UseDelay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public decimal DelaySeconds
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 30;

    public bool? DialogResult
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string CharacterCount
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "0/60";

    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public RequestMessageDialogViewModel()
    {
        var canConfirm = this.WhenAnyValue(x => x.Message)
            .Select(m => !string.IsNullOrWhiteSpace(m));

        OkCommand = ReactiveCommand.Create(() => DialogResult = true, canConfirm);
        CancelCommand = ReactiveCommand.Create(() => DialogResult = false);

        this.WhenAnyValue(x => x.Message)
            .Select(m => string.Format(CultureInfo.CurrentCulture, UiStrings.Dialog_CharacterCountFormat, m.Length))
            .Subscribe(c => CharacterCount = c);
    }
}
