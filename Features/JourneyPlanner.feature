Feature: TfL Journey Planner widget
  As a traveller in London
  I want to plan journeys with the "Plan a journey" widget on tfl.gov.uk
  So that I can see how long my journey will take and what access facilities are on the way

  Background:
    Given I am on the TfL "Plan a journey" page

  @smoke @valid-journey
  Scenario: Plan a valid journey between two Underground stations using the autocomplete suggestions
    When I plan a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station" selecting both from the autocomplete suggestions
    Then the journey results are shown from "Leicester Square Underground Station" to "Covent Garden Underground Station"
    And a walking time in minutes is shown for the journey
    And a cycling time in minutes is shown for the journey
    And the cycling time is no longer than the walking time

  @valid-journey @preferences
  Scenario: Update the planned journey to routes with least walking
    When I plan a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station" selecting both from the autocomplete suggestions
    And I edit my preferences to select routes with least walking and update the journey
    Then the travel preferences summary shows "Showing routes with least walking"
    And at least one journey option is shown
    And the total journey time of the first option matches its departure and arrival times

  @valid-journey @accessibility
  Scenario: View details of the planned journey and check the access information at the destination station
    When I plan a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station" selecting both from the autocomplete suggestions
    And I edit my preferences to select routes with least walking and update the journey
    And I view the details of the first journey option
    Then the complete access information shown at "Covent Garden Underground Station" is:
      | Access information |
      | Up stairs          |
      | Up lift            |
      | Level walkway      |

  @invalid-journey
  Scenario Outline: The widget does not provide results when an invalid journey is planned
    When I plan a journey from "<from>" to "<to>" by typing the locations without using the suggestions
    Then no journey options are shown
    And a message is shown that the location could not be matched

    # Note: TfL fuzzy-matches free text. Some nonsense strings (e.g. "zzzzqqqq") are silently resolved to a
    # real place and a journey is planned anyway (see README, "Observations about the live site"), so the
    # strings below were chosen because the site cannot resolve them to a single location.
    Examples:
      | description              | from                                 | to                                |
      | both locations invalid   | notarealplace99                      | zzzz-invalid-zzzz                 |
      | only the From is invalid | notarealplace99                      | Covent Garden Underground Station |
      | only the To is invalid   | Leicester Square Underground Station | zzzz-invalid-zzzz                 |

  @invalid-journey @validation
  Scenario: The widget is unable to plan a journey when no locations are entered
    When I submit the journey planner without entering any locations
    Then I remain on the "Plan a journey" page
    And the From field shows the validation error "The From field is required."
    And the To field shows the validation error "The To field is required."
