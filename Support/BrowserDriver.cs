using Microsoft.Playwright;

namespace TflJourneyPlanner.Tests.Support;

/// <summary>
/// Owns the Playwright browser, context and page for a single scenario.
/// One instance is created per scenario by the Reqnroll DI container, which keeps
/// scenarios fully isolated (fresh cookies, storage and tracing for each one).
/// </summary>
public sealed class BrowserDriver : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public TestSettings Settings { get; } = TestSettings.Current;

    public IPage Page => _page ?? throw new InvalidOperationException("Browser has not been started. Call StartAsync first.");

    public IBrowserContext Context => _context ?? throw new InvalidOperationException("Browser has not been started. Call StartAsync first.");

    public async Task StartAsync(string traceTitle)
    {
        _playwright = await Playwright.CreateAsync();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = Settings.Headless,
            SlowMo = Settings.SlowMoMs,
        };
        if (!string.IsNullOrWhiteSpace(Settings.BrowserChannel))
        {
            // "chromium" = Playwright's full Chromium build in new headless mode (default here);
            // "chrome" or "msedge" drive a locally installed branded browser instead.
            launchOptions.Channel = Settings.BrowserChannel;
        }

        _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = Settings.ViewportWidth, Height = Settings.ViewportHeight },
            Locale = "en-GB",
            TimezoneId = "Europe/London",
        });
        _context.SetDefaultTimeout(Settings.DefaultTimeoutMs);

        await _context.Tracing.StartAsync(new TracingStartOptions
        {
            Title = traceTitle,
            Screenshots = true,
            Snapshots = true,
            Sources = false,
        });

        _page = await _context.NewPageAsync();
    }

    public async Task<string?> SaveTraceAsync(string filePath)
    {
        if (_context is null) return null;
        await _context.Tracing.StopAsync(new TracingStopOptions { Path = filePath });
        return filePath;
    }

    public async Task<string?> SaveScreenshotAsync(string filePath)
    {
        if (_page is null || _page.IsClosed) return null;
        await _page.ScreenshotAsync(new PageScreenshotOptions { Path = filePath, FullPage = true });
        return filePath;
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) await _context.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
