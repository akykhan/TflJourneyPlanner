using Microsoft.Playwright;
using TflJourneyPlanner.Tests.Support;

namespace TflJourneyPlanner.Tests.Pages;

/// <summary>Page object for the "Plan a journey" widget at /plan-a-journey.</summary>
public sealed class JourneyPlannerPage(IPage page, TestSettings settings)
{
    // Canonical URL with the trailing slash: the slash-less form is redirected by the site most of the time but
    // sometimes answered with an empty 200 instead (see NavigateWithRetryAsync).
    public const string RelativeUrl = "/plan-a-journey/";

    private ILocator FromInput => page.Locator("#InputFrom");
    private ILocator ToInput => page.Locator("#InputTo");
    private ILocator PlanMyJourneyButton => page.Locator("#plan-journey-button");
    private ILocator FromError => page.Locator("#InputFrom-error");
    private ILocator ToError => page.Locator("#InputTo-error");

    public async Task OpenAsync()
    {
        await NavigateWithRetryAsync(settings.BaseUrl + RelativeUrl);
        await FromInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await new CookieBanner(page).DismissIfPresentAsync();
    }

    /// <summary>
    /// tfl.gov.uk intermittently answers with an HTTP 200 that has an empty body (confirmed in saved Playwright
    /// traces and with plain HTTP requests), which leaves a blank page. The response body is checked directly so an
    /// empty page is retried immediately instead of waiting for a selector timeout. Genuine failures still surface:
    /// after the last attempt the normal timeout applies to the widget appearing.
    /// </summary>
    private async Task NavigateWithRetryAsync(string url, int attempts = 3)
    {
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            var body = response is null ? string.Empty : await response.TextAsync();
            if (!string.IsNullOrWhiteSpace(body)) return;

            Console.WriteLine($"[JourneyPlannerPage] Empty response from {url} (attempt {attempt} of {attempts}); retrying.");
            await page.WaitForTimeoutAsync(1000 * attempt);
        }
    }

    /// <summary>
    /// Types a partial search term into the From field and picks the matching suggestion
    /// from the "Stations, stops and piers" autocomplete list, as a real user would.
    /// </summary>
    public Task SelectFromUsingAutocompleteAsync(string fullLocationName) =>
        SelectUsingAutocompleteAsync(FromInput, "InputFrom", fullLocationName);

    public Task SelectToUsingAutocompleteAsync(string fullLocationName) =>
        SelectUsingAutocompleteAsync(ToInput, "InputTo", fullLocationName);

    /// <summary>Enters free text without using the suggestions, e.g. to submit an invalid location.</summary>
    public async Task EnterFromTextAsync(string text) => await EnterTextAsync(FromInput, text);

    public async Task EnterToTextAsync(string text) => await EnterTextAsync(ToInput, text);

    public async Task ClickPlanMyJourneyAsync() => await PlanMyJourneyButton.ClickAsync();

    public async Task<string> GetFromValidationErrorAsync() => (await FromError.InnerTextAsync()).Trim();

    public async Task<string> GetToValidationErrorAsync() => (await ToError.InnerTextAsync()).Trim();

    public ILocator FromValidationError => FromError;
    public ILocator ToValidationError => ToError;

    private async Task SelectUsingAutocompleteAsync(ILocator input, string inputId, string fullLocationName)
    {
        var partialTerm = PartialSearchTerm(fullLocationName);
        await input.ClickAsync();
        await input.FillAsync(partialTerm);

        var suggestions = page.Locator($"#{inputId}-dropdown .tt-suggestion .stop-name");
        var match = suggestions.Filter(new LocatorFilterOptions { HasText = fullLocationName }).First;
        await match.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await match.ClickAsync();

        // The widget writes the selected suggestion back into the input; make sure that has happened before moving on.
        await Assertions.Expect(input).ToHaveValueAsync(fullLocationName);
    }

    private static async Task EnterTextAsync(ILocator input, string text)
    {
        await input.ClickAsync();
        await input.FillAsync(text);
        // Close the suggestion dropdown (if any) so it cannot overlap the submit button.
        await input.PressAsync("Escape");
    }

    /// <summary>
    /// Deliberately returns only the first 12 characters, e.g. "Leicester Sq", so the test genuinely
    /// relies on the autocomplete suggestion rather than typing the complete station name.
    /// </summary>
    internal static string PartialSearchTerm(string fullLocationName) =>
        fullLocationName.Length <= 12 ? fullLocationName : fullLocationName[..12].TrimEnd();
}
