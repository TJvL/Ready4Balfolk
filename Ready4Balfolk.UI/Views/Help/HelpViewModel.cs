using System.Threading;
using ReactiveUI;

namespace Ready4Balfolk.UI.Views.Help;

public sealed class HelpViewModel : ReactiveObject
{
    public string HelpSource { get; } = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "nl"
        ? "avares://Ready4Balfolk.UI/Views/Help/help.nl.md"
        : "avares://Ready4Balfolk.UI/Views/Help/help.md";
}
