using Hardened.Shared.Runtime.Application;
using Xunit;

namespace LambdaWidgets.Runtime.Tests;

/// <summary>
/// The page a widget answers with when its handler threw.
/// </summary>
/// <remarks>
/// Direct rather than through an invocation, because the switch is the part worth pinning and a
/// handler cannot be thrown from twice under two environments in one test host.
/// </remarks>
public class WhatAFailedInvocationShowsTests {
    private static readonly Exception Thrown =
        new InvalidOperationException("Throughput exceeded on table deliveries");

    /// <summary>
    /// Off by default, because this page reaches a real dashboard and the message is the function
    /// author's rather than the viewer's.
    /// </summary>
    [Fact]
    public void TheExceptionsMessageIsNotOnThePageByDefault() {
        var page = new WidgetErrors().Page(Thrown, "req-1");

        Assert.DoesNotContain("Throughput exceeded", page);
        Assert.Contains("req-1", page);
    }

    /// <summary>
    /// And there is a switch, which is the difference between a five-second fix and reading the
    /// test tool's output for a message the page could have shown.
    /// </summary>
    [Fact]
    public void TheSwitchPutsTheMessageOnThePage() {
        var page = new WidgetErrors(Detail("1")).Page(Thrown, "req-1");

        Assert.Contains("Throughput exceeded on table deliveries", page);
    }

    /// <summary>A message carrying markup is data, not markup.</summary>
    [Fact]
    public void TheMessageIsEscaped() {
        var page = new WidgetErrors(Detail("true"))
            .Page(new InvalidOperationException("<script>go()</script>"), "req-1");

        Assert.DoesNotContain("<script>", page);
        Assert.Contains("&lt;script&gt;", page);
    }

    /// <summary>
    /// The marker the console interpreter reads to tell this page from a widget's own. Both arrive
    /// as a body, so nothing else distinguishes them.
    /// </summary>
    [Fact]
    public void ThePageIsMarkedWithWhatFailed() {
        Assert.Contains(
            $"{WidgetErrors.Marker}=\"InvalidOperationException\"",
            new WidgetErrors().Page(Thrown, "req-1"));
    }

    private static IHardenedEnvironment Detail(string value) =>
        new EnvironmentImpl(environmentValues: new Dictionary<string, string> {
            [WidgetErrors.DetailVariable] = value
        });
}
