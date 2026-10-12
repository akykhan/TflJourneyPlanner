# TfL Journey Planner – UI test automation framework

Automated UI tests for the **Plan a journey** widget on [tfl.gov.uk/plan-a-journey](https://tfl.gov.uk/plan-a-journey),
built for the Senior Test Analyst coding challenge.

| | |
|---|---|
| Language | C# / .NET 8 |
| Browser automation | [Microsoft.Playwright](https://playwright.dev/dotnet/) 1.63 (Chromium) |
| BDD / Gherkin | [Reqnroll](https://reqnroll.net/) 3.3 (the actively maintained successor of SpecFlow) |
| Test runner | NUnit 4 |

## Use of AI – disclaimer

This submission was produced with the help of an AI coding assistant (Claude Code by Anthropic).
The AI was used to scaffold the project, draft the page objects, step definitions, hooks, feature file,
this README and the GitHub Actions workflow, and to investigate the live page structure (selectors, cookie
banner, asynchronous loading behaviour) by running throwaway exploration scripts against the site.
Every generated piece of code was then reviewed, run against the live website and adjusted by me, and the
design decisions and test scenarios below reflect my own judgement about what is worth testing and how.

## What is covered

All scenarios live in [`Features/JourneyPlanner.feature`](Features/JourneyPlanner.feature).

| # | Challenge requirement | Scenario | Tag |
|---|---|---|---|
| 1 | Plan a valid journey Leicester Square → Covent Garden, choosing both stations from the **autocomplete suggestions** (partial text only); validate walking and cycling times | *Plan a valid journey between two Underground stations using the autocomplete suggestions* | `@valid-journey @smoke` |
| 2 | *Edit preferences* → *Routes with least walking* → *Update journey*; validate the journey time | *Update the planned journey to routes with least walking* | `@preferences` |
| 3 | *View details* and verify the complete access information at Covent Garden Underground Station | *View details of the planned journey and check the access information at the destination station* | `@accessibility` |
| 4 | No results for an invalid journey (one or more invalid locations) | *The widget does not provide results when an invalid journey is planned* (scenario outline, 3 examples: both invalid / From invalid / To invalid) | `@invalid-journey` |
| 5 | Cannot plan a journey when no locations are entered | *The widget is unable to plan a journey when no locations are entered* | `@validation` |

That is 5 scenarios / 7 executable tests. The "additional scenarios" the brief asks for (not automated) are
listed at the end of this file.

## Running the tests

Prerequisites: the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/akykhan/TflJourneyPlanner.git
cd TflJourneyPlanner
dotnet build

# one-off: download the Chromium build Playwright drives
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
#   – or, without PowerShell:
dotnet tool install --global Microsoft.Playwright.CLI && playwright install chromium

dotnet test
```

Useful variations:

```bash
dotnet test --filter "Category=smoke"            # run by Gherkin tag
dotnet test --filter "Category=invalid-journey"
TFL_HEADLESS=false dotnet test                   # watch the browser
TFL_SLOWMO_MS=300 TFL_HEADLESS=false dotnet test # slow it down
TFL_BROWSER_CHANNEL=chrome dotnet test           # run on an installed Google Chrome instead of bundled Chromium
```

Settings live in [`testsettings.json`](testsettings.json) (base URL, headless, timeouts, viewport, browser
channel) and every environment variable above overrides its JSON counterpart, so the same build runs headed on a
laptop and headless in CI without code changes.

When a scenario fails, a full-page screenshot and a Playwright **trace** are written to
`bin/<config>/net8.0/TestArtifacts/` and attached to the NUnit result. A trace is a complete recording of the
scenario (DOM snapshots, network, console, every action) and opens with `playwright show-trace <file>.trace.zip`.

A GitHub Actions workflow ([`.github/workflows/tests.yml`](.github/workflows/tests.yml)) builds, installs
Chromium and runs the suite on every push, uploading the TRX results and any failure artifacts.

## Project structure

```
TflJourneyPlanner/            (project file: TflJourneyPlanner.Tests.csproj)
├── Features/JourneyPlanner.feature      Gherkin scenarios (the executable specification)
├── StepDefinitions/JourneyPlannerSteps.cs  Thin glue: one method per step, assertions only
├── Pages/
│   ├── JourneyPlannerPage.cs            The widget: inputs, autocomplete, submit, validation messages
│   ├── JourneyResultsPage.cs            Results, Edit preferences panel, View details, access information
│   └── CookieBanner.cs                  Cookiebot consent overlay handling
├── Hooks/PlaywrightHooks.cs             Browser per scenario, DI registration, failure artifacts
├── Support/
│   ├── BrowserDriver.cs                 Playwright lifecycle + tracing
│   ├── TestSettings.cs                  testsettings.json + environment-variable overrides
│   └── TextParsing.cs                   "11mins" / "12:09 - 12:20" → numbers
├── testsettings.json, reqnroll.json
└── .github/workflows/tests.yml
```

## Development decisions

**Playwright rather than Selenium + ChromeDriver.** The brief mentions both "Playwright" and "Chromedriver".
Playwright ships and manages its own browser driver, so no separate ChromeDriver binary (and no version-matching
between the driver and the installed Chrome) is needed – that fragility is one of the main reasons to prefer
Playwright. The suite drives Chromium through Playwright; setting `BrowserChannel` to `chrome` or `msedge`
drives a locally installed branded browser if that is ever required. Playwright also gives auto-waiting
locators, web-first assertions (`Expect(...)`), and tracing for free, which removed every `Thread.Sleep`-style
wait from the code.

**Reqnroll rather than SpecFlow.** SpecFlow is no longer maintained (its last release was 2023 and support ended
in 2024). Reqnroll is the open-source fork by the original SpecFlow team, API-compatible, and actively released,
so it is the responsible choice for a new framework in 2026.

**Page Object Model with thin steps.** Step definitions contain only orchestration and assertions; everything
that knows a CSS selector lives in a page object. When TfL changes the markup only one file needs to change.
Selectors prefer stable ids and semantic hooks (`#InputFrom`, `role=button[name="Update journey"]`,
`aria-label` on the access icons, the `data-from`/`data-to` attributes the page itself exposes) over layout
classes.

**A fresh browser context per scenario, injected through Reqnroll's DI container.** Each scenario starts with
no cookies or storage, so scenarios are independent and can be run in any order or individually. Page objects
and the Playwright `IPage` are registered in the scenario container and arrive in the step class through
constructor injection – no static state anywhere.

**Scenarios build on the user journey, not on each other.** Scenarios 2 and 3 re-plan the journey rather than
depending on scenario 1 having run, which is what makes them runnable in isolation (`--filter`) and keeps
failures attributable to one scenario.

**Assertions are derived from the page, not hard-coded against live data.** The site is live; journey times and
fares change by the minute. So the tests assert *properties* that must always hold: the walking and cycling
times are present and positive and cycling is not slower than walking; after choosing "least walking" the
preference summary says so and the first option's stated total time equals the gap between its displayed
departure and arrival times. The access information at Covent Garden is the one place where a fixed expectation
is appropriate (it is station infrastructure, not timetable data) and it is kept in a Gherkin table so a
non-developer can update it if TfL changes the station.

**Genuine use of the autocomplete.** The step types only the first 12 characters of the station name
(`Leicester Sq`, `Covent Garde`) and then clicks the matching entry under *Stations, stops and piers*, then
asserts the input now holds the full station name. The suggestion list contains bus stops with similar names,
so the match is on the full station name rather than "first suggestion".

**Cookie banner handled with a Playwright locator handler.** tfl.gov.uk injects a Cookiebot overlay about half a
second after the page loads; it covers the page and intercepts every click, and in roughly one run in twenty it
appeared late or re-appeared after it had been dismissed. Rather than a one-off "click accept" at the start (which is
what made early runs flaky), the framework registers a locator handler for the overlay: whenever it is visible while
an action is waiting, Playwright dismisses it first. An eager dismissal still runs when a page is opened so the
initial state and screenshots are clean. (Pre-seeding the `CookieConsent` cookie was tried and does not suppress the
banner; the consent stamp is validated.) Separately, *Accept only essential cookies* leaves the dimming overlay on
the page – a usability bug worth raising.

**Resilience to the live site without hiding real failures.** Early runs failed intermittently with a blank
planner page. The saved Playwright trace showed an HTTP 200 with an *empty body*, and a quick probe with plain HTTP
requests showed why: `https://tfl.gov.uk/plan-a-journey` (no trailing slash) is answered half the time with a 301 to
the canonical `/plan-a-journey/` and half the time with an empty 200. The page object therefore navigates to the
canonical URL (which returned the full page on every probe) and, as a safety net, checks the response body and
retries an empty response up to three times before applying the normal timeouts. Results load asynchronously
("Fetching results…"), so the results page waits for either a result or an error/disambiguation message rather
than for a fixed delay.

**Chromium's new headless mode.** `BrowserChannel` defaults to `chromium`, which runs the full Chromium build
in the new headless mode rather than the stripped-down headless shell; it behaves identically to a headed
browser, which matters for a public site behind Cloudflare.

**Scenarios run sequentially.** Parallel execution is trivial to enable with NUnit, but this suite drives a
public website, so it is deliberately kept to one browser at a time to be a good citizen and to avoid
rate-limiting-induced flakiness.

**Configuration over code.** `testsettings.json` plus environment variables; nothing environment-specific is
compiled in.

## Observations about the live site (found while building the tests)

These are things a tester would raise, discovered while automating:

1. **Nonsense locations can be silently "resolved".** The journey API fuzzy-matches free text. For some strings
   (e.g. `zzzzqqqq`, `InvalidLocation123`) it picks a single real place without telling the user and plans a
   journey from there – in one run `zzzzqqqq` became a 68-minute trip starting near Bromley. For other strings it
   correctly shows *"We found more than one location matching…"*. The invalid-journey scenario therefore uses
   inputs the site cannot resolve, and this behaviour is called out in the feature file.
2. **The site never says "no results".** An unrecognised location always produces a disambiguation list of weak
   matches (even for pure gibberish) rather than a clear "we could not find that location" message.
3. **"Accept only essential cookies" blocks the page.** The dark overlay stays on screen after declining, so the
   widget cannot be used until the page is reloaded.
4. **The non-canonical planner URL intermittently returns an empty page.** `GET /plan-a-journey` (without the
   trailing slash) alternates between a 301 to `/plan-a-journey/?cid=plan-a-journey` and an empty `200` with no
   content type. Any link or bookmark to the slash-less URL will show users a blank page roughly half the time.
5. **Walking and cycling times are only offered for short journeys.** For Leicester Square → Covent Garden the
   results page shows a *Walking and cycling* panel (walk 6 min, cycle 1 min) with the advice "If you can, walk
   or cycle this route"; after switching to *least walking* the panel changes to *Cycling and other options*.

## Additional scenarios I would test (not automated)

### Functional

- Autocomplete: suggestions appear after a minimum number of characters; grouped correctly (stations / bus
  stops / points of interest / postcodes); keyboard navigation (arrow keys, Enter, Escape); selecting a
  suggestion populates the field; no suggestions message; very long input (100-character `maxlength`).
- Location types: postcode, street address, point of interest, "current location" (geolocation granted /
  denied), locations outside London with and without the *Search outside London* option.
- Swap From/To button exchanges the values; "Travel via" adds a via point that appears in the itinerary.
- Leaving/arriving time: *change time* with a future date and time, an arrive-by time, a past time (warning),
  times across midnight and daylight-saving changes, dates beyond the published timetable.
- Journey preferences: fastest / fewest changes / least walking each change the ordering and the summary
  text; mode filters (e.g. Tube only, bus only, deselect all → validation); maximum walking time and walking
  speed affect the walking legs; *I'd rather walk if it makes my journey quicker*.
- Accessibility preferences: step-free to platform / full step-free access produce only step-free itineraries
  and the access icons agree with the station's published accessibility data.
- Results content: departure/arrival times are consistent with leg durations; fare shown and consistent with
  peak/off-peak time; *Earlier/Later journeys* paginate correctly; *Map view* opens the map; *View directions*
  shows turn-by-turn walking instructions; *Edit journey* returns to the form pre-populated.
- Disruption handling: planned closures and live disruptions appear on the affected leg and alternative routes
  are offered.
- Favourites / Recents: saving a journey, recent journeys list, behaviour when functionality cookies are
  declined.
- Validation: whitespace-only input, identical From and To, HTML/script characters (XSS), SQL-like input,
  unicode and accented place names.
- Same journey via the URL query string (deep links shared by users) renders the same results as the widget.

### Non-functional

- **Performance**: time to first suggestion in the autocomplete (target < 500 ms), time to render results,
  behaviour of the results page under slow 3G throttling, no layout shift while "Fetching results".
- **Load / resilience**: the widget degrades gracefully when the Journey API is slow or returns 5xx
  (user-facing message, no infinite spinner); retry behaviour; rate limiting of the autocomplete endpoint.
- **Accessibility (WCAG 2.2 AA)**: full keyboard operation of the widget and results accordion; screen-reader
  announcements for suggestions (`aria-live`), validation errors and loading state; colour contrast of line
  colours and error text; focus management after *Update journey* and *View details*; access icons have
  text alternatives (they do – `aria-label`, verified by scenario 3).
- **Cross-browser / device**: Chrome, Edge, Firefox, Safari (Playwright can run all four), iOS Safari and
  Android Chrome, responsive layout at 320 px, touch interaction with the suggestion list.
- **Security**: inputs are encoded in the results page and in the query string (no reflected XSS); no
  sensitive data in URLs beyond the journey itself; cookie consent is honoured (no marketing cookies before
  consent); third-party scripts loaded only after consent.
- **Privacy / compliance**: geolocation only after explicit permission; recent journeys stored locally are
  cleared when functionality cookies are withdrawn.
- **Localisation**: Google Translate integration on the page does not break the widget; 24-hour time format;
  dates in en-GB.
- **Analytics**: tracking events fire for plan / edit preferences / view details with the correct values
  (the markup carries `data-tracking` attributes).
- **Observability in the test framework itself**: trace and screenshot on failure (done), run in CI on a
  schedule to detect site changes early, flakiness dashboard.

## Known limitations

- The tests run against the live, public TfL website, so they are subject to real-world variability
  (timetable, disruptions, Cloudflare). Mitigations are described above; a nightly CI run would surface drift
  early.
- The access-information expectation for Covent Garden (*Up stairs, Up lift, Level walkway*) reflects the station
  at the time of writing.
