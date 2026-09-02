using Microsoft.Playwright;

namespace Ready4Balfolk.E2E;

/// <summary>A browser on the other side of the room, opened at one of the served pages.</summary>
/// <remarks>
/// The pages are what a laptop at the projector and a phone in a pocket actually run, so a scenario
/// opens them rather than calling the hubs behind them: a hub answering correctly says nothing about
/// what anybody in the hall can read.
/// </remarks>
public sealed class TheBrowser : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;

    private TheBrowser(IPlaywright playwright, IBrowser browser, IPage page)
    {
        _playwright = playwright;
        _browser = browser;
        Page = page;
    }

    /// <summary>The page itself, for the steps that click and the assertions that read.</summary>
    public IPage Page { get; }

    /// <summary>
    /// Fetches the browser itself, once, if this machine has not got it yet.
    /// </summary>
    /// <remarks>
    /// Through Playwright's own installer rather than the shell script beside it, because that
    /// script needs PowerShell and a developer machine is not required to have any.
    /// </remarks>
    private static readonly Lazy<int> Fetched = new(() => Program.Main(["install", "chromium"]));

    /// <summary>Makes sure the browser is on this machine, from a process that can still reach out.</summary>
    /// <remarks>
    /// Called by the parent before it starts a scenario, and never by the scenario itself. A
    /// scenario's world has no internet, so a download attempted inside one goes to a proxy that is
    /// not listening: on a machine that already had the browser this looked fine for days, and the
    /// first cold build agent failed every web scenario on "Download failure".
    /// </remarks>
    public static void FetchIfThisMachineHasNotGotIt() =>
        Assert.Equal(0, Fetched.Value);

    /// <summary>Opens a browser at this address and waits for the page to settle.</summary>
    public static async Task<TheBrowser> OpenAt(string address)
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(address);

        return new TheBrowser(playwright, browser, page);
    }

    /// <summary>What the element with this id says, once it says anything.</summary>
    public async Task<string> Reads(string elementId) =>
        await Page.Locator($"#{elementId}").InnerTextAsync();

    /// <summary>Waits for an element to read this, which is how a page following an evening behaves.</summary>
    public async Task WaitUntilItReads(string elementId, string text) =>
        await Page.Locator($"#{elementId}").Filter(new LocatorFilterOptions { HasTextString = text })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

    /// <summary>Types into a box on the page, the way a thumb does.</summary>
    public async Task TypeInto(string elementId, string text) =>
        await Page.Locator($"#{elementId}").FillAsync(text);

    /// <summary>Taps something on the page.</summary>
    public async Task Tap(string elementId) =>
        await Page.Locator($"#{elementId}").ClickAsync();

    /// <summary>Holds something down, for the buttons that ask to be held rather than tapped.</summary>
    /// <remarks>
    /// Skipping is one of them: a mis-tap on a phone is instantly audible to a whole room, so the
    /// page makes it a hold, and a scenario that tapped it would be testing nothing.
    /// </remarks>
    public async Task HoldDown(string elementId, TimeSpan howLong)
    {
        var button = Page.Locator($"#{elementId}");

        await button.HoverAsync();
        await Page.Mouse.DownAsync();
        await Task.Delay(howLong);
        await Page.Mouse.UpAsync();
    }

    /// <summary>Waits until the page is showing one of these, whichever it lands on.</summary>
    public async Task SettlesOnEither(string oneId, string orTheOtherId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            if (await IsShowing(oneId) || await IsShowing(orTheOtherId))
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"The page showed neither {oneId} nor {orTheOtherId}.");
    }

    /// <summary>Whether the page is showing this at all.</summary>
    public async Task<bool> IsShowing(string elementId) =>
        await Page.Locator($"#{elementId}").IsVisibleAsync();

    public async ValueTask DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}
