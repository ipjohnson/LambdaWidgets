using Xunit;

namespace LambdaWidgets.Dashboard.Tests;

/// <summary>
/// The mistakes the console makes no noise about.
///
/// <para>
/// Every one of these produces a widget that renders and then does nothing, which is the worst
/// failure mode a widget has: it looks finished. Direct rather than through
/// <see cref="IWidgetConsole"/>, because "what is wrong with this HTML" has one answer and no
/// composition to exercise.
/// </para>
/// </summary>
public class WhatTheLinterFindsTests {
    private static readonly IWidgetLinter Linter =
        new WidgetLinter(new WidgetActions(), new WidgetSanitizer());

    private static string Rule(string html) => Assert.Single(Linter.Lint(html)).Rule;

    // ------------------------------------------------------------------ what the console removes

    /// <summary>
    /// The one an author hits first, because it is how every other web page responds to a click.
    /// The console strips it and says nothing, so the button renders and does nothing.
    /// </summary>
    [Fact]
    public void AnOnClickIsReported() {
        Assert.Equal(WidgetLinter.RemovedHandler, Rule("""<a onclick="go()">Go</a>"""));
    }

    [Theory]
    [InlineData("<script>go()</script>")]
    [InlineData("<svg><use href='#x'></use></svg>")]
    public void AnElementTheConsoleRemovesIsReported(string html) {
        Assert.Contains(
            Linter.Lint(html), finding => finding.Rule == WidgetLinter.RemovedElement);
    }

    [Fact]
    public void AJavaScriptUrlIsReported() {
        Assert.Equal(WidgetLinter.RemovedUrl, Rule("""<a href="javascript:go()">Go</a>"""));
    }

    // ------------------------------------------------------------------ actions that never fire

    /// <summary>
    /// A cwdb-action binds its previous sibling. Anything between the two breaks the binding, and
    /// the console reports nothing.
    /// </summary>
    [Fact]
    public void AnActionWithNothingBeforeItIsReported() {
        Assert.Equal(WidgetLinter.UnboundAction, Rule("<cwdb-action>hi</cwdb-action>"));
    }

    [Fact]
    public void ACallWithNoEndpointIsReported() {
        Assert.Equal(
            WidgetLinter.CallWithoutEndpoint,
            Rule("""<a>Go</a><cwdb-action action="call">{}</cwdb-action>"""));
    }

    /// <summary>The console allows mouseenter only on an action that displays HTML.</summary>
    [Fact]
    public void MouseEnterOnACallIsReported() {
        Assert.Equal(
            WidgetLinter.MouseEnterOnCall,
            Rule("""<a>Go</a><cwdb-action action="call" endpoint="e" event="mouseenter">{}</cwdb-action>"""));
    }

    /// <summary>
    /// Content that is not JSON sends no parameters at all, including the route — so the click
    /// reaches the landing page rather than doing nothing, which is harder to notice.
    /// </summary>
    [Fact]
    public void ACallWhoseContentIsNotJsonIsReported() {
        Assert.Equal(
            WidgetLinter.CallContentIsNotJson,
            Rule("""<a>Go</a><cwdb-action action="call" endpoint="e">route=/search</cwdb-action>"""));
    }

    // ------------------------------------------------------------------ fields that never travel

    /// <summary>
    /// The field renders, the viewer types in it, and the click carries everything except that.
    /// </summary>
    [Theory]
    [InlineData("<input value='x'>")]
    [InlineData("<textarea>x</textarea>")]
    [InlineData("<select><option>x</option></select>")]
    public void AFieldWithNoNameIsReported(string html) {
        Assert.Equal(WidgetLinter.FieldWithoutName, Rule(html));
    }

    /// <summary>
    /// A submit button is not a field a widget reads back, and reporting one would train an author
    /// to ignore the rule.
    /// </summary>
    [Fact]
    public void AButtonWithNoNameIsNotReported() {
        Assert.Empty(Linter.Lint("""<input type="submit" value="Go">"""));
    }

    // ------------------------------------------------------------------ CSS that reaches too far

    /// <summary>
    /// Every widget on a dashboard shares one page, so a bare element selector restyles whatever
    /// its neighbours rendered. AWS's own samples write these, which only works if the console
    /// isolates each widget - and nothing AWS publishes says it does.
    /// </summary>
    [Theory]
    [InlineData("<style>td { white-space: nowrap; }</style>")]
    [InlineData("<style>table.rows td { color: red; }</style>")]
    [InlineData("<style>svg { height: 100%; }</style>")]
    [InlineData("<style>h2, .panel { margin: 0; }</style>")]
    public void ASelectorThatCanMatchAnotherWidgetIsReported(string html) {
        Assert.Equal(WidgetLinter.UnscopedSelector, Rule(html));
    }

    /// <summary>A selector confined by a class of the widget's own reaches nothing else.</summary>
    [Theory]
    [InlineData("<style>.rows td { white-space: nowrap; }</style>")]
    [InlineData("<style>#chart-1 .bar { fill: red; }</style>")]
    [InlineData("<style>.a td, .b th { padding: 0; }</style>")]
    [InlineData("<style>.card > .title { font-weight: 700; }</style>")]
    public void AScopedSelectorIsNotReported(string html) {
        Assert.Empty(Linter.Lint(html));
    }

    /// <summary>
    /// An at-rule's prelude is a condition rather than a selector, and the rules inside it are
    /// judged on their own.
    /// </summary>
    [Fact]
    public void AMediaQueryIsJudgedByWhatIsInsideIt() {
        Assert.Empty(Linter.Lint("<style>@media (min-width: 400px) { .rows td { padding: 0; } }</style>"));

        Assert.Equal(
            WidgetLinter.UnscopedSelector,
            Rule("<style>@media (min-width: 400px) { td { padding: 0; } }</style>"));
    }

    /// <summary>The same selector twice is one thing to fix, not two.</summary>
    [Fact]
    public void ARepeatedSelectorIsReportedOnce() {
        Assert.Single(Linter.Lint("<style>td { color: red; } td { padding: 0; }</style>"));
    }

    /// <summary>A selector inside a comment is not a rule.</summary>
    [Fact]
    public void ACommentedOutSelectorIsNotReported() {
        Assert.Empty(Linter.Lint("<style>/* td { color: red; } */ .rows td { color: blue; }</style>"));
    }

    // ------------------------------------------------------------------ nothing wrong

    /// <summary>
    /// The shape the Razor helpers emit. A linter that reported anything here would be a linter
    /// nobody left switched on.
    /// </summary>
    [Fact]
    public void AWellFormedWidgetHasNothingWrongWithIt() {
        Assert.Empty(Linter.Lint("""
            <form><input name="query" value="fields @timestamp"></form>
            <a class="btn btn-primary">Run query</a>
            <cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:1:function:f">
              { "route": "/search" }
            </cwdb-action>
            <a>What does this do?</a>
            <cwdb-action display="popup"><b>It searches logs.</b></cwdb-action>
            <style>.w-rows td { white-space: nowrap; }</style>
            """));
    }

    /// <summary>Two mistakes are two findings, so a count means something.</summary>
    [Fact]
    public void EveryMistakeIsReportedSeparately() {
        Assert.Equal(2, Linter.Lint("""<input value="x"><a onclick="go()">Go</a>""").Count);
    }
}
