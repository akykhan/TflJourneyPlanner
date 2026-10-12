using System.Text.Json;

namespace TflJourneyPlanner.Tests.Support;

/// <summary>
/// Strongly typed test configuration. Values come from testsettings.json and can be
/// overridden with environment variables (TFL_BASE_URL, TFL_HEADLESS, TFL_BROWSER_CHANNEL, TFL_SLOWMO_MS)
/// so the same build can run locally with a visible browser and headless in CI.
/// </summary>
public sealed class TestSettings
{
    public string BaseUrl { get; init; } = "https://tfl.gov.uk";
    public bool Headless { get; init; } = true;
    public string BrowserChannel { get; init; } = "";
    public int SlowMoMs { get; init; }
    public int DefaultTimeoutMs { get; init; } = 30000;
    public int ResultsTimeoutMs { get; init; } = 60000;
    public int ViewportWidth { get; init; } = 1366;
    public int ViewportHeight { get; init; } = 900;
    public string ArtifactsDirectory { get; init; } = "TestArtifacts";

    private static readonly Lazy<TestSettings> Instance = new(Load);

    public static TestSettings Current => Instance.Value;

    private static TestSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "testsettings.json");
        var fromFile = File.Exists(path)
            ? JsonSerializer.Deserialize<TestSettings>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : null;
        fromFile ??= new TestSettings();

        return new TestSettings
        {
            BaseUrl = Env("TFL_BASE_URL") ?? fromFile.BaseUrl,
            Headless = bool.TryParse(Env("TFL_HEADLESS"), out var headless) ? headless : fromFile.Headless,
            BrowserChannel = Env("TFL_BROWSER_CHANNEL") ?? fromFile.BrowserChannel,
            SlowMoMs = int.TryParse(Env("TFL_SLOWMO_MS"), out var slowMo) ? slowMo : fromFile.SlowMoMs,
            DefaultTimeoutMs = fromFile.DefaultTimeoutMs,
            ResultsTimeoutMs = fromFile.ResultsTimeoutMs,
            ViewportWidth = fromFile.ViewportWidth,
            ViewportHeight = fromFile.ViewportHeight,
            ArtifactsDirectory = fromFile.ArtifactsDirectory,
        };
    }

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
