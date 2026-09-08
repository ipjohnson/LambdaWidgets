using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Testing;
using Xunit;

namespace WidgetName.Tests;

/// <summary>
/// The widget, driven the way a viewer drives it.
/// </summary>
/// <remarks>
/// Every test here says what the widget should do, never how it does it. It opens the widget, types
/// into a field by its name, clicks by the text on screen, and asserts on what came back.
/// </remarks>
public class LookingItemsUpTests {

    [HardenedTest]
    public async Task WhatTheViewerTypedIsWhatIsLookedUp(
        IWidgetDriver widget, StandInLookups table) {
        widget.Params["table"] = "orders";

        await widget.Open();
        widget.Fill("partitionKey", "customer#42");

        await widget.Click("Look up");

        Assert.Equal("orders", table.Asked!.Value.Table);
        Assert.Equal("customer#42", table.Asked!.Value.PartitionKey);
    }

    /// <summary>
    /// The dashboard's table wins over anything in the request. A widget whose table a viewer could
    /// change is a widget whose execution role has to allow every table.
    /// </summary>
    [HardenedTest]
    public async Task TheDashboardsTableCannotBeOverriddenByAClick(
        IWidgetDriver widget, StandInLookups table) {
        widget.Params["table"] = "orders";

        await widget.Open();
        await widget.Click("Look up");

        Assert.Equal("orders", table.Asked!.Value.Table);
    }

    [HardenedTest]
    public async Task TheItemsThatCameBackAreWhatIsShown(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer#42");

        Assert.Contains("placed", (await widget.Click("Look up")).Html);
    }

    /// <summary>
    /// Paging with no session at all: the cursor leaves in the link the viewer clicks and comes
    /// back in the next request. Nothing is remembered between invocations.
    /// </summary>
    [HardenedTest]
    public async Task NextCarriesTheViewerToThePageAfterIt(
        IWidgetDriver widget, StandInLookups table) {
        await widget.Open();
        widget.Fill("partitionKey", "customer#42");

        await widget.Click("Look up");
        var second = await widget.Click("Next");

        Assert.Equal(["", "cursor-to-page-2"], table.Cursors);
        Assert.Contains("shipped", second.Html);
    }

    /// <summary>The last page offers no Next, because there is nothing after it.</summary>
    [HardenedTest]
    public async Task TheLastPageDoesNotOfferAnother(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer#42");

        await widget.Click("Look up");

        Assert.DoesNotContain("Next", (await widget.Click("Next")).Actions.Select(one => one.Text));
    }

    /// <summary>
    /// A refresh puts the widget back on its landing page. The console keeps no state between
    /// invocations, and a widget that pretended otherwise would surprise its author.
    /// </summary>
    [HardenedTest]
    public async Task ARefreshReturnsTheWidgetToItsLandingPage(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer#42");
        await widget.Click("Look up");

        Assert.DoesNotContain("Back", (await widget.Refresh()).Actions.Select(one => one.Text));
    }

    /// <summary>Nothing the widget renders is something the console would strip.</summary>
    [HardenedTest]
    public async Task NothingTheWidgetRendersIsThrownAway(IWidgetDriver widget) =>
        Assert.Empty((await widget.Open()).Findings);
}
