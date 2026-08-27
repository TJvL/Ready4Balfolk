using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.UI;

public sealed class App : Application, IApplicationAppearance
{
    /// <summary>The container, for the places Avalonia constructs an object itself.</summary>
    /// <remarks>
    /// Views are built by the XAML loader, which offers no constructor to inject through, so a
    /// handful of code-behind files resolve what they need from here. Set once from
    /// <see cref="Program"/>'s builder and never afterwards, which is what the private setter is
    /// for: it used to be publicly settable, so anything could swap the container out at any point.
    /// </remarks>
    internal static IServiceProvider Services { get; private set; } = null!;

    internal static void UseServices(IServiceProvider services) => Services = services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services.GetRequiredService<ApplicationStartup>().Run(desktop, this);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // A resource rather than a property on each control: the App.axaml style pushes it into every
    // ButtonContent at once, so call sites only supply their icon and label.
    public void ApplyShowButtonText(bool showText) => Resources["ShowButtonText"] = showText;

    public void ApplyTheme(ApplicationTheme theme) =>
        RequestedThemeVariant = theme switch
        {
            ApplicationTheme.Light => ThemeVariant.Light,
            ApplicationTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
}
