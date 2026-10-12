using NUnit.Framework;
using Reqnroll;
using Reqnroll.BoDi;
using TflJourneyPlanner.Tests.Pages;
using TflJourneyPlanner.Tests.Support;

namespace TflJourneyPlanner.Tests.Hooks;

/// <summary>
/// Starts a fresh browser for every scenario and registers the page objects in the scenario container
/// so step classes can simply ask for them in their constructors.
/// On failure a full-page screenshot and a Playwright trace are saved and attached to the NUnit result.
/// </summary>
[Binding]
public sealed class PlaywrightHooks(IObjectContainer container, ScenarioContext scenarioContext)
{
    private BrowserDriver? _driver;

    [BeforeScenario(Order = 0)]
    public async Task StartBrowserAsync()
    {
        _driver = new BrowserDriver();
        await _driver.StartAsync(scenarioContext.ScenarioInfo.Title);
        await CookieBanner.RegisterAutoDismissAsync(_driver.Page);

        container.RegisterInstanceAs(_driver);
        container.RegisterInstanceAs(_driver.Settings);
        container.RegisterInstanceAs(_driver.Page);
        container.RegisterInstanceAs(new JourneyPlannerPage(_driver.Page, _driver.Settings));
        container.RegisterInstanceAs(new JourneyResultsPage(_driver.Page, _driver.Settings));
    }

    [AfterScenario(Order = 0)]
    public async Task CaptureFailureArtifactsAndStopBrowserAsync()
    {
        if (_driver is null) return;

        try
        {
            if (scenarioContext.TestError is not null)
            {
                var dir = Path.Combine(AppContext.BaseDirectory, _driver.Settings.ArtifactsDirectory);
                Directory.CreateDirectory(dir);
                var stem = $"{SafeFileName(scenarioContext.ScenarioInfo.Title)}_{DateTime.Now:yyyyMMdd_HHmmss}";

                var screenshot = await _driver.SaveScreenshotAsync(Path.Combine(dir, stem + ".png"));
                var trace = await _driver.SaveTraceAsync(Path.Combine(dir, stem + ".trace.zip"));

                if (screenshot is not null) TestContext.AddTestAttachment(screenshot, "Screenshot at failure");
                if (trace is not null) TestContext.AddTestAttachment(trace, "Playwright trace (open with: playwright show-trace)");
                TestContext.Out.WriteLine($"Failure artifacts written to {dir}");
            }
        }
        finally
        {
            await _driver.DisposeAsync();
        }
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());
        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }
}
