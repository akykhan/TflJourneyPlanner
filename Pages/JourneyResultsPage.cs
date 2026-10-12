using Microsoft.Playwright;
using TflJourneyPlanner.Tests.Support;

namespace TflJourneyPlanner.Tests.Pages;

/// <summary>Page object for /plan-a-journey/results, including the Edit preferences panel and expanded journey details.</summary>
public sealed class JourneyResultsPage(IPage page, TestSettings settings)
{
    // Summary header
    private ILocator SummaryData => page.Locator("#journey-summary-data");
    private ILocator TravelPreferencesSummary => page.Locator(".travel-preferences .scroller");

    // Results are fetched asynchronously after the page loads. Any of these means the fetch has finished.
    private ILocator AnyResultOrMessage => page.Locator(".journey-box, .journey-option, .disambiguation-box, .journey-results .info-message, .no-results, .field-validation-error:visible");
    private ILocator FetchingResultsIndicator => page.Locator("#loader-window-wrapper :visible, .loader-window:visible");

    // "Walking and cycling" panel shown for short journeys
    private ILocator WalkingTime => page.Locator(".journey-box.walking .journey-info");
    private ILocator CyclingTime => page.Locator(".journey-box.cycling .journey-info");

    // Public transport journey options
    private ILocator JourneyOptions => page.Locator(".journey-option");
    private ILocator FirstJourneyOption => JourneyOptions.First;
    private ILocator FirstJourneyTotalTime => FirstJourneyOption.Locator(".journey-time");
    private ILocator FirstJourneyDepartArrive => FirstJourneyOption.Locator(".time-box .time");
    private ILocator FirstViewDetailsButton => page.Locator("button.view-hide-details").First;
    private ILocator FirstJourneyDetails => page.Locator("#option-1-content");

    // Edit preferences panel
    private ILocator EditPreferencesButton => page.Locator("button.more-options");
    private ILocator PreferencesPanel => page.Locator("#more-journey-options");
    private ILocator LeastWalkingOption => PreferencesPanel.Locator("label[for='JourneyPreference_2']");
    private ILocator LeastWalkingRadio => PreferencesPanel.Locator("#JourneyPreference_2");
    private ILocator UpdateJourneyButton => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Update journey" });

    // No results / invalid journey
    private ILocator DisambiguationMessage => page.Locator(".disambiguation-box .info-message, .journey-results .info-message, .no-results");

    public async Task WaitForResultsToLoadAsync()
    {
        await page.WaitForURLAsync("**/plan-a-journey/results**");
        await new CookieBanner(page).DismissIfPresentAsync();
        await AnyResultOrMessage.First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = settings.ResultsTimeoutMs,
        });
    }

    public async Task<string> GetFromAsync() => await SummaryData.GetAttributeAsync("data-from") ?? string.Empty;

    public async Task<string> GetToAsync() => await SummaryData.GetAttributeAsync("data-to") ?? string.Empty;

    public async Task<int> GetWalkingMinutesAsync() => TextParsing.ExtractMinutes(await WalkingTime.InnerTextAsync());

    public async Task<int> GetCyclingMinutesAsync() => TextParsing.ExtractMinutes(await CyclingTime.InnerTextAsync());

    public ILocator WalkingTimeLocator => WalkingTime;
    public ILocator CyclingTimeLocator => CyclingTime;

    public async Task SelectLeastWalkingAndUpdateAsync()
    {
        await EditPreferencesButton.ClickAsync();
        await PreferencesPanel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await LeastWalkingOption.ClickAsync();
        await Assertions.Expect(LeastWalkingRadio).ToBeCheckedAsync();
        await UpdateJourneyButton.ClickAsync();

        await page.WaitForURLAsync("**JourneyPreference=leastwalking**");
        await WaitForResultsToLoadAsync();
    }

    public async Task<string> GetTravelPreferencesSummaryAsync() => await TravelPreferencesSummary.InnerTextAsync();

    public ILocator TravelPreferencesSummaryLocator => TravelPreferencesSummary;

    public async Task<int> GetJourneyOptionCountAsync() => await JourneyOptions.CountAsync();

    public async Task<int> GetFirstJourneyTotalMinutesAsync() =>
        TextParsing.ExtractMinutes(await FirstJourneyTotalTime.InnerTextAsync());

    public async Task<(TimeSpan Depart, TimeSpan Arrive)> GetFirstJourneyDepartArriveAsync() =>
        TextParsing.ExtractDepartArrive(await FirstJourneyDepartArrive.InnerTextAsync());

    public async Task ViewDetailsOfFirstJourneyAsync()
    {
        await FirstViewDetailsButton.ClickAsync();
        await FirstJourneyDetails.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Returns the access information (e.g. "Up stairs", "Up lift", "Level walkway") shown for the
    /// journey step that starts at the given location in the expanded journey details.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAccessInformationAtAsync(string locationName)
    {
        var step = FirstJourneyDetails
            .Locator(".journey-detail-step")
            .Filter(new LocatorFilterOptions
            {
                Has = page.Locator(".start-location .location-name", new PageLocatorOptions { HasTextString = locationName }),
            });

        await Assertions.Expect(step).ToHaveCountAsync(1);

        var icons = step.Locator(".access-information a[aria-label]");
        var labels = new List<string>();
        foreach (var icon in await icons.AllAsync())
        {
            var label = await icon.GetAttributeAsync("aria-label");
            if (!string.IsNullOrWhiteSpace(label)) labels.Add(label.Trim());
        }
        return labels;
    }

    public ILocator DisambiguationMessageLocator => DisambiguationMessage;

    public ILocator JourneyOptionsLocator => JourneyOptions;
}
