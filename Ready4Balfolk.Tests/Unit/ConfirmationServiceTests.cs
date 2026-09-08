using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The fallback a confirmation dialog's buttons use when a caller does not name them. An omitted
/// <c>confirmText</c> or <c>cancelText</c> has to resolve to the "Yes" or "No" resource, not to an
/// empty string a broken null-coalesce would let through unnoticed.
/// </summary>
public sealed class ConfirmationServiceTests
{
    [Fact]
    public void AnOmittedConfirmText_ResolvesToTheYesResource()
    {
        var resolved = ConfirmationService.ResolveConfirmText(null);

        Assert.Equal(UiStrings.Dialog_YesDefault, resolved);
        Assert.False(string.IsNullOrWhiteSpace(resolved));
    }

    [Fact]
    public void AnOmittedCancelText_ResolvesToTheNoResource()
    {
        var resolved = ConfirmationService.ResolveCancelText(null);

        Assert.Equal(UiStrings.Dialog_NoDefault, resolved);
        Assert.False(string.IsNullOrWhiteSpace(resolved));
    }

    [Fact]
    public void AnExplicitConfirmText_PassesThroughUnchanged()
    {
        var resolved = ConfirmationService.ResolveConfirmText("Reload the folder?");

        Assert.Equal("Reload the folder?", resolved);
    }

    [Fact]
    public void AnExplicitCancelText_PassesThroughUnchanged()
    {
        var resolved = ConfirmationService.ResolveCancelText("Not now");

        Assert.Equal("Not now", resolved);
    }
}
