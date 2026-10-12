using Microsoft.Playwright;

namespace TflJourneyPlanner.Tests.Pages;

/// <summary>
/// tfl.gov.uk shows a Cookiebot consent overlay that is injected asynchronously (~0.5 s after the page loads) and
/// sits on top of the whole page, swallowing clicks. During exploration it occasionally appeared late or re-appeared
/// after being dismissed, so relying on a single up-front dismissal was flaky.
///
/// Two layers handle it:
///  1. <see cref="RegisterAutoDismissAsync"/> installs a Playwright locator handler: whenever the overlay is visible
///     while an action is waiting for its target to be actionable, Playwright runs the handler first. This is
///     Playwright's recommended way to deal with overlays that can show up at any time.
///  2. <see cref="DismissIfPresentAsync"/> dismisses the banner eagerly when a page is opened, so that screenshots and
///     the initial state are clean.
/// Each scenario runs in a fresh browser context, so the banner appears once per scenario.
/// </summary>
public sealed class CookieBanner(IPage page)
{
    private const string OverlaySelector = "#cb-cookieoverlay";
    private const string AcceptAllSelector = "#CybotCookiebotDialogBodyLevelButtonLevelOptinAllowAll";

    private ILocator Overlay => page.Locator(OverlaySelector);
    private ILocator AcceptAllButton => page.Locator(AcceptAllSelector);

    public static async Task RegisterAutoDismissAsync(IPage page)
    {
        var banner = new CookieBanner(page);
        await page.AddLocatorHandlerAsync(
            banner.Overlay,
            async _ => await banner.CloseAsync(),
            new PageAddLocatorHandlerOptions { NoWaitAfter = true });
    }

    public async Task DismissIfPresentAsync()
    {
        try
        {
            await AcceptAllButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            return; // Not shown (yet); the locator handler covers a late arrival.
        }

        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (await AcceptAllButton.IsVisibleAsync())
        {
            await AcceptAllButton.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
            try
            {
                await Overlay.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 5000 });
                return;
            }
            catch (TimeoutException)
            {
                // fall through to the last resort below
            }
        }

        // The dimming overlay is still there without a usable dialog (observed once in ~20 runs: the overlay was
        // re-shown with aria-hidden="true" after consent had been given). It is a third-party widget, not the
        // system under test, so hide it rather than fail the scenario, and say so in the output.
        Console.WriteLine("[CookieBanner] Consent overlay still present without a dialog; hiding it.");
        await page.EvaluateAsync($"() => {{ const o = document.querySelector('{OverlaySelector}'); if (o) o.style.display = 'none'; }}");
    }
}
