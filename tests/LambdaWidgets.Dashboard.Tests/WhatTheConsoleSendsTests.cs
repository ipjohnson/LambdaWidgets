using Hardened.Shared.Testing.Attributes;
using Xunit;

namespace LambdaWidgets.Dashboard.Tests;

/// <summary>
/// The invocation a viewer's action produces.
///
/// <para>
/// These are the console's requirements rather than any one service's behaviour, so they go through
/// <see cref="IWidgetConsole"/> as the harness and the test driver do. A widget author reading a
/// failure here learns what the console does; that it took a parser, a form reader and an event
/// builder to answer is not the subject.
/// </para>
/// </summary>
public class WhatTheConsoleSendsTests {
    private static readonly DashboardWidget Search = new() {
        Id = "widget-3",
        Endpoint = "arn:aws:lambda:us-east-1:012345678901:function:customWidgetLogsSearch",
        Title = "Log search",
        Params = new Dictionary<string, string> { ["logGroups"] = "/aws/lambda/orders", ["limit"] = "20" }
    };

    private static readonly DashboardState Ops = new() {
        Name = "ops",
        AccountId = "012345678901",
        Theme = Theme.Dark,
        TimeRange = WidgetTimeRange.Relative(
            TimeSpan.FromHours(3), DateTimeOffset.Parse("2026-09-07T12:00:00Z"))
    };

    // ------------------------------------------------------------------ appearing and refreshing

    [HardenedTest]
    public void AWidgetOpensWithItsConfiguredParameters(IWidgetConsole console) {
        var opened = console.Opens(Search, Ops);

        Assert.Equal("/aws/lambda/orders", opened.Fields["logGroups"]);
        Assert.Equal("20", opened.Fields["limit"]);
    }

    /// <summary>
    /// The behaviour that catches every widget author once. A refresh, a resize and a time range
    /// change all re-invoke with the widget's <em>configured</em> parameters, so a viewer three
    /// pages into a tool is returned to its landing page. Nothing carries the page they were on,
    /// because the console keeps no state between invocations.
    /// </summary>
    [HardenedTest]
    public void ARefreshReturnsTheWidgetToItsLandingPage(IWidgetConsole console) {
        var shown = console.Shows(Answer("""
            <a class="btn">Page 2</a>
            <cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:1:function:f">
              { "route": "/results", "cursor": "abc" }
            </cwdb-action>
            """), Ops);

        var navigated = console.Clicks(shown, 0, NoEdits, Search, Ops);

        Assert.Equal("/results", navigated.Fields["route"]);

        var refreshed = console.Refreshes(Search, Ops);

        Assert.False(refreshed.Fields.ContainsKey("route"));
        Assert.False(refreshed.Fields.ContainsKey("cursor"));
    }

    // ------------------------------------------------------------------ clicking

    [HardenedTest]
    public void AClickSendsTheActionsParametersOverTheConfiguredOnes(IWidgetConsole console) {
        var shown = console.Shows(Answer("""
            <a class="btn">Next</a>
            <cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:1:function:f">
              { "limit": "50" }
            </cwdb-action>
            """), Ops);

        var clicked = console.Clicks(shown, 0, NoEdits, Search, Ops);

        Assert.Equal("50", clicked.Fields["limit"]);
        Assert.Equal("/aws/lambda/orders", clicked.Fields["logGroups"]);
    }

    /// <summary>
    /// The fields travel under <c>widgetContext.forms.all</c> rather than at the root, which is
    /// what lets a handler tell what the viewer typed from what the widget was configured with.
    /// </summary>
    [HardenedTest]
    public void AClickCarriesTheWidgetsFormFields(IWidgetConsole console) {
        var shown = console.Shows(Answer("""
            <input name="query" value="fields @timestamp">
            <a class="btn">Run</a>
            <cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:1:function:f">{}</cwdb-action>
            """), Ops);

        var clicked = console.Clicks(shown, 0, NoEdits, Search, Ops);

        Assert.Equal("fields @timestamp", clicked.Context.Forms["query"]);
    }

    [HardenedTest]
    public void AFieldTheViewerEditedBeatsTheOneTheFunctionRendered(IWidgetConsole console) {
        var shown = console.Shows(Answer("""
            <input name="query" value="fields @timestamp">
            <a class="btn">Run</a>
            <cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:1:function:f">{}</cwdb-action>
            """), Ops);

        var clicked = console.Clicks(
            shown, 0, new Dictionary<string, string> { ["query"] = "stats count(*)" }, Search, Ops);

        Assert.Equal("stats count(*)", clicked.Context.Forms["query"]);
    }

    /// <summary>
    /// A browser submits nothing for an unchecked box rather than submitting false, so a handler
    /// tests for the key's presence. Sending "false" would make every such handler wrong.
    /// </summary>
    [HardenedTest]
    public void AnUncheckedBoxSendsNothing(IWidgetConsole console) {
        var shown = console.Shows(Answer("""
            <input type="checkbox" name="live">
            <input type="checkbox" name="verbose" checked>
            <a class="btn">Run</a>
            <cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:1:function:f">{}</cwdb-action>
            """), Ops);

        var clicked = console.Clicks(shown, 0, NoEdits, Search, Ops);

        Assert.False(clicked.Context.Forms.ContainsKey("live"));
        Assert.Equal("on", clicked.Context.Forms["verbose"]);
    }

    /// <summary>
    /// An action bound to an element the sanitizer removed is not on screen, so a viewer cannot
    /// reach it and neither can a driver. Reading actions from the raw response instead would let a
    /// test click something the console never showed.
    ///
    /// <para>
    /// <c>use</c> rather than <c>iframe</c> or <c>script</c>, and that took a probe to get right.
    /// The content of those two is parsed as text by any HTML parser, so an action written inside
    /// one is never an element and this test passed against a mutation that read the raw response.
    /// SVG content is parsed as elements, so <c>use</c> is the one removal of the three where the
    /// distinction is real.
    /// </para>
    /// </summary>
    [HardenedTest]
    public void AnActionInsideAStrippedElementCannotBeClicked(IWidgetConsole console) {
        var shown = console.Shows(Answer("""
            <svg><use><a class="btn">Hidden</a>
            <cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:1:function:f">{}</cwdb-action>
            </use></svg>
            """), Ops);

        Assert.Empty(shown.Actions);
    }

    /// <summary>The same for a field: what is not on screen does not travel with a click.</summary>
    [HardenedTest]
    public void AFieldInsideAStrippedElementIsNotSent(IWidgetConsole console) {
        var shown = console.Shows(Answer("""
            <svg><use><input name="secret" value="v"></use></svg>
            <a class="btn">Run</a>
            <cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:1:function:f">{}</cwdb-action>
            """), Ops);

        Assert.False(console.Clicks(shown, 0, NoEdits, Search, Ops).Context.Forms.ContainsKey("secret"));
    }

    /// <summary>
    /// An <c>html</c> action has content to show and no function to call. Answering with an
    /// invocation would send one to whatever endpoint happened to be nearby.
    /// </summary>
    [HardenedTest]
    public void ClickingAnActionThatOnlyShowsHtmlIsRefused(IWidgetConsole console) {
        var shown = console.Shows(Answer("""
            <a>What does this do?</a>
            <cwdb-action display="popup"><b>It searches logs.</b></cwdb-action>
            """), Ops);

        Assert.Throws<InvalidOperationException>(
            () => console.Clicks(shown, 0, NoEdits, Search, Ops));
    }

    // ------------------------------------------------------------------ the dashboard's own state

    [HardenedTest]
    public void TheDashboardsTimeRangeReachesTheFunction(IWidgetConsole console) {
        var opened = console.Opens(Search, Ops);

        Assert.Equal(DateTimeOffset.Parse("2026-09-07T09:00:00Z"), opened.Context.TimeRange.Start);
        Assert.Equal(DateTimeOffset.Parse("2026-09-07T12:00:00Z"), opened.Context.TimeRange.End);
    }

    /// <summary>
    /// A viewer who zooms a chart expects the widget beside it to follow. The AWS sample reads
    /// <c>timeRange.zoom || timeRange</c>, and <c>Effective</c> is that: a widget reading
    /// <c>Start</c> directly ignores the zoom the viewer just did.
    /// </summary>
    [HardenedTest]
    public void AZoomedChartIsTheRangeAWidgetShouldQuery(IWidgetConsole console) {
        var zoomed = Ops with {
            TimeRange = Ops.TimeRange.ZoomedTo(
                DateTimeOffset.Parse("2026-09-07T11:00:00Z"),
                DateTimeOffset.Parse("2026-09-07T11:30:00Z"))
        };

        var range = console.Opens(Search, zoomed).Context.TimeRange;

        Assert.Equal(DateTimeOffset.Parse("2026-09-07T11:00:00Z"), range.Effective.Start);
        Assert.Equal(DateTimeOffset.Parse("2026-09-07T11:30:00Z"), range.Effective.End);
    }

    // ------------------------------------------------------------------ documentation

    /// <summary>
    /// The console's <em>Get documentation</em> button. A function answering describe returns
    /// markdown and does nothing else, so the flag has to be on the event for it to know.
    /// </summary>
    [HardenedTest]
    public void DocumentationIsAskedForWithTheDescribeFlag(IWidgetConsole console) {
        var asked = console.AsksForDocumentation(Search, Ops);

        Assert.True(asked.Describe);
        Assert.Contains("\"describe\":true", asked.ToJson());
    }

    [HardenedTest]
    public void AnOrdinaryInvocationDoesNotCarryTheDescribeFlag(IWidgetConsole console) {
        Assert.DoesNotContain("describe", console.Opens(Search, Ops).ToJson());
    }

    private static readonly Dictionary<string, string> NoEdits = new();

    /// <summary>
    /// An Invoke response carrying HTML. The API returns JSON, so a function's HTML string arrives
    /// quoted and escaped — which is the detail that surprises anyone reading a raw response, and
    /// the reason a test cannot just hand the interpreter its HTML.
    /// </summary>
    private static string Answer(string html) =>
        System.Text.Json.JsonSerializer.Serialize(html);
}
