using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;
using Reqnroll;
using TflJourneyPlanner.Tests.Pages;
using TflJourneyPlanner.Tests.Support;

namespace TflJourneyPlanner.Tests.StepDefinitions;

[Binding]
public sealed class JourneyPlannerSteps(
    IPage page,
    JourneyPlannerPage plannerPage,
    JourneyResultsPage resultsPage)
{
    [Given(@"I am on the TfL ""Plan a journey"" page")]
    public async Task GivenIAmOnThePlanAJourneyPage()
    {
        await plannerPage.OpenAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/plan-a-journey/?(\?.*)?$"));
    }

    [When(@"I plan a journey from ""(.*)"" to ""(.*)"" selecting both from the autocomplete suggestions")]
    public async Task WhenIPlanAJourneyUsingAutocomplete(string from, string to)
    {
        await plannerPage.SelectFromUsingAutocompleteAsync(from);
        await plannerPage.SelectToUsingAutocompleteAsync(to);
        await plannerPage.ClickPlanMyJourneyAsync();
        await resultsPage.WaitForResultsToLoadAsync();
    }

    [When(@"I plan a journey from ""(.*)"" to ""(.*)"" by typing the locations without using the suggestions")]
    public async Task WhenIPlanAJourneyByTypingText(string from, string to)
    {
        await plannerPage.EnterFromTextAsync(from);
        await plannerPage.EnterToTextAsync(to);
        await plannerPage.ClickPlanMyJourneyAsync();
        await resultsPage.WaitForResultsToLoadAsync();
    }

    [When(@"I submit the journey planner without entering any locations")]
    public async Task WhenISubmitWithoutLocations()
    {
        await plannerPage.ClickPlanMyJourneyAsync();
    }

    [When(@"I edit my preferences to select routes with least walking and update the journey")]
    public async Task WhenISelectLeastWalkingAndUpdate()
    {
        await resultsPage.SelectLeastWalkingAndUpdateAsync();
    }

    [When(@"I view the details of the first journey option")]
    public async Task WhenIViewDetailsOfTheFirstJourney()
    {
        await resultsPage.ViewDetailsOfFirstJourneyAsync();
    }

    [Then(@"the journey results are shown from ""(.*)"" to ""(.*)""")]
    public async Task ThenTheJourneyResultsAreShownFromTo(string from, string to)
    {
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/plan-a-journey/results"));
        var actualFrom = await resultsPage.GetFromAsync();
        var actualTo = await resultsPage.GetToAsync();
        Assert.Multiple(() =>
        {
            Assert.That(actualFrom, Is.EqualTo(from), "'From' location in the results summary");
            Assert.That(actualTo, Is.EqualTo(to), "'To' location in the results summary");
        });
    }

    [Then(@"a walking time in minutes is shown for the journey")]
    public async Task ThenAWalkingTimeIsShown()
    {
        await Assertions.Expect(resultsPage.WalkingTimeLocator).ToBeVisibleAsync();
        var minutes = await resultsPage.GetWalkingMinutesAsync();
        Assert.That(minutes, Is.GreaterThan(0), "Walking time in minutes");
        TestContext.Out.WriteLine($"Walking time: {minutes} mins");
    }

    [Then(@"a cycling time in minutes is shown for the journey")]
    public async Task ThenACyclingTimeIsShown()
    {
        await Assertions.Expect(resultsPage.CyclingTimeLocator).ToBeVisibleAsync();
        var minutes = await resultsPage.GetCyclingMinutesAsync();
        Assert.That(minutes, Is.GreaterThan(0), "Cycling time in minutes");
        TestContext.Out.WriteLine($"Cycling time: {minutes} mins");
    }

    [Then(@"the cycling time is no longer than the walking time")]
    public async Task ThenCyclingIsNoLongerThanWalking()
    {
        var walking = await resultsPage.GetWalkingMinutesAsync();
        var cycling = await resultsPage.GetCyclingMinutesAsync();
        Assert.That(cycling, Is.LessThanOrEqualTo(walking), $"Cycling ({cycling} mins) should not take longer than walking ({walking} mins)");
    }

    [Then(@"the travel preferences summary shows ""(.*)""")]
    public async Task ThenTheTravelPreferencesSummaryShows(string expectedText)
    {
        await Assertions.Expect(resultsPage.TravelPreferencesSummaryLocator).ToContainTextAsync(expectedText);
    }

    [Then(@"at least one journey option is shown")]
    public async Task ThenAtLeastOneJourneyOptionIsShown()
    {
        await Assertions.Expect(resultsPage.JourneyOptionsLocator.First).ToBeVisibleAsync();
        Assert.That(await resultsPage.GetJourneyOptionCountAsync(), Is.GreaterThanOrEqualTo(1));
    }

    [Then(@"the total journey time of the first option matches its departure and arrival times")]
    public async Task ThenTheJourneyTimeMatchesDepartureAndArrival()
    {
        var totalMinutes = await resultsPage.GetFirstJourneyTotalMinutesAsync();
        var (depart, arrive) = await resultsPage.GetFirstJourneyDepartArriveAsync();
        var elapsed = TextParsing.MinutesBetween(depart, arrive);

        TestContext.Out.WriteLine($"First option: {depart:hh\\:mm} - {arrive:hh\\:mm}, total {totalMinutes} mins");
        Assert.Multiple(() =>
        {
            Assert.That(totalMinutes, Is.GreaterThan(0), "Journey time in minutes");
            Assert.That(totalMinutes, Is.EqualTo(elapsed), $"Journey time should equal the gap between departure {depart:hh\\:mm} and arrival {arrive:hh\\:mm}");
        });
    }

    [Then(@"the complete access information shown at ""(.*)"" is:")]
    public async Task ThenTheAccessInformationAtIs(string location, DataTable expected)
    {
        var expectedLabels = expected.Rows.Select(r => r[0].Trim()).ToList();
        var actualLabels = await resultsPage.GetAccessInformationAtAsync(location);

        TestContext.Out.WriteLine($"Access information at {location}: {string.Join(", ", actualLabels)}");
        Assert.That(actualLabels, Is.EqualTo(expectedLabels).AsCollection,
            $"Complete access information at {location} (order as displayed)");
    }

    [Then(@"no journey options are shown")]
    public async Task ThenNoJourneyOptionsAreShown()
    {
        await Assertions.Expect(resultsPage.JourneyOptionsLocator).ToHaveCountAsync(0);
        await Assertions.Expect(resultsPage.WalkingTimeLocator).ToHaveCountAsync(0);
        await Assertions.Expect(resultsPage.CyclingTimeLocator).ToHaveCountAsync(0);
    }

    [Then(@"a message is shown that the location could not be matched")]
    public async Task ThenALocationNotMatchedMessageIsShown()
    {
        var message = resultsPage.DisambiguationMessageLocator.First;
        await Assertions.Expect(message).ToBeVisibleAsync();
        var text = await message.InnerTextAsync();
        TestContext.Out.WriteLine($"Message shown: {text.Trim()}");
        Assert.That(text, Does.Contain("location").IgnoreCase, "Message should explain the problem with the entered location");
    }

    [Then(@"I remain on the ""Plan a journey"" page")]
    public async Task ThenIRemainOnThePlanAJourneyPage()
    {
        await Assertions.Expect(page).Not.ToHaveURLAsync(new Regex(@"/plan-a-journey/results"));
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/plan-a-journey"));
    }

    [Then(@"the From field shows the validation error ""(.*)""")]
    public async Task ThenTheFromFieldShowsError(string expected)
    {
        await Assertions.Expect(plannerPage.FromValidationError).ToBeVisibleAsync();
        Assert.That(await plannerPage.GetFromValidationErrorAsync(), Is.EqualTo(expected));
    }

    [Then(@"the To field shows the validation error ""(.*)""")]
    public async Task ThenTheToFieldShowsError(string expected)
    {
        await Assertions.Expect(plannerPage.ToValidationError).ToBeVisibleAsync();
        Assert.That(await plannerPage.GetToValidationErrorAsync(), Is.EqualTo(expected));
    }
}
