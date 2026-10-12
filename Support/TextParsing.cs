using System.Globalization;
using System.Text.RegularExpressions;

namespace TflJourneyPlanner.Tests.Support;

/// <summary>Small helpers for turning on-screen text such as "11mins" or "12:09 - 12:20" into values we can assert on.</summary>
public static partial class TextParsing
{
    [GeneratedRegex(@"(\d+)\s*min", RegexOptions.IgnoreCase)]
    private static partial Regex MinutesRegex();

    [GeneratedRegex(@"\b(\d{1,2}:\d{2})\b")]
    private static partial Regex ClockTimeRegex();

    public static int ExtractMinutes(string text)
    {
        var match = MinutesRegex().Match(text);
        if (!match.Success)
            throw new FormatException($"Could not find a duration in minutes in the text '{text}'.");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Extracts the first two clock times (e.g. depart and arrive) from text such as "Depart at: 12:09 - Arrive at: 12:20".</summary>
    public static (TimeSpan Depart, TimeSpan Arrive) ExtractDepartArrive(string text)
    {
        var matches = ClockTimeRegex().Matches(text);
        if (matches.Count < 2)
            throw new FormatException($"Expected two clock times in the text '{text}' but found {matches.Count}.");
        return (TimeSpan.Parse(matches[0].Groups[1].Value, CultureInfo.InvariantCulture),
                TimeSpan.Parse(matches[1].Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Minutes between two clock times, allowing for a journey that crosses midnight.</summary>
    public static int MinutesBetween(TimeSpan depart, TimeSpan arrive)
    {
        var diff = arrive - depart;
        if (diff < TimeSpan.Zero) diff += TimeSpan.FromDays(1);
        return (int)diff.TotalMinutes;
    }
}
