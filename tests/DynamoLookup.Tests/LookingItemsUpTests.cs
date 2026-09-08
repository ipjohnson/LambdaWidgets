using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Testing;
using Xunit;

namespace DynamoLookup.Tests;

/// <summary>
/// The lookup widget, and the thing it exists to demonstrate: paging with no session.
/// </summary>
public class LookingItemsUpTests {

    // ------------------------------------------------------------------ looking up

    [HardenedTest]
    public async Task TheViewersKeyIsWhatIsLookedUp(IWidgetDriver widget, StandInLookups table) {
        widget.Params["table"] = "orders";

        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        await widget.Click("Look up");

        Assert.Equal("customer-42", table.Asked!.Value.PartitionKey);
        Assert.Equal("orders", table.Asked!.Value.Table);
    }

    /// <summary>
    /// A widget whose table a viewer could change is a widget whose role has to allow every table,
    /// which is the opposite of what scoping a role is for.
    /// </summary>
    [HardenedTest]
    public async Task AViewerCannotChangeTheTable(IWidgetDriver widget, StandInLookups table) {
        widget.Params["table"] = "orders";

        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        await widget.Click("Look up");

        Assert.Equal("orders", table.Asked!.Value.Table);
    }

    [HardenedTest]
    public async Task ASortPrefixNarrowsTheLookup(IWidgetDriver widget, StandInLookups table) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");
        widget.Fill("sortPrefix", "2026-09");

        await widget.Click("Look up");

        Assert.Equal("2026-09", table.Asked!.Value.SortPrefix);
    }

    [HardenedTest]
    public async Task TheItemsAreShownAsATable(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        var found = await widget.Click("Look up");

        Assert.Contains("placed", found.Html);
        Assert.Contains("customer-42", found.Html);
    }

    /// <summary>A widget is too narrow for a row worth reading.</summary>
    [HardenedTest]
    public async Task EachItemOpensInFull(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        Assert.Contains(
            (await widget.Click("Look up")).Actions, action => action.Kind == ActionKind.Html);
    }

    // ------------------------------------------------------------------ paging, which is the point

    /// <summary>
    /// The sample's whole reason for existing. A widget gets no cookies, no local storage and no
    /// state between invocations, so where the viewer is up to is a property of the link they are
    /// about to click and of nothing else.
    /// </summary>
    [HardenedTest]
    public async Task WhereTheViewerIsUpToRidesInTheLink(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        var first = await widget.Click("Look up");

        var next = Assert.Single(first.Actions, action => action.Text == "Next");

        Assert.Equal("cursor-to-page-2", next.Parameters["cursor"]);
    }

    /// <summary>And clicking it carries that cursor back, which is the round trip.</summary>
    [HardenedTest]
    public async Task ClickingNextAsksForWhereItLeftOff(
        IWidgetDriver widget, StandInLookups table) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        await widget.Click("Look up");
        await widget.Click("Next");

        Assert.Equal(["", "cursor-to-page-2"], table.Cursors);
    }

    [HardenedTest]
    public async Task TheSecondPageShowsItsOwnItems(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        await widget.Click("Look up");

        Assert.Contains("shipped", (await widget.Click("Next")).Html);
    }

    /// <summary>
    /// The last page offers no Next, because a link that asked for nothing would look like more.
    /// </summary>
    [HardenedTest]
    public async Task TheLastPageOffersNoNext(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        await widget.Click("Look up");

        Assert.DoesNotContain((await widget.Click("Next")).Actions, action => action.Text == "Next");
    }

    /// <summary>
    /// A refresh loses the page the viewer was on, because the console re-invokes with the
    /// configured parameters and the cursor was never among them. Asserted rather than lamented:
    /// it is what the console does, and a paging widget has to be designed for it.
    /// </summary>
    [HardenedTest]
    public async Task ARefreshLosesThePageTheViewerWasOn(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        await widget.Click("Look up");

        Assert.DoesNotContain((await widget.Refresh()).Actions, action => action.Text == "Next");
    }

    // ------------------------------------------------------------------ what the console sees

    [HardenedTest]
    public async Task NothingTheWidgetEmitsIsFaultyOrStripped(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("partitionKey", "customer-42");

        var found = await widget.Click("Look up");

        Assert.Empty(found.Removals);
        Assert.Empty(found.Actions.SelectMany(action => action.Faults));
    }

    [HardenedTest]
    public async Task TheDocumentationOffersItsParameters(IWidgetDriver widget) {
        Assert.Contains(
            "table:", new WidgetDocumentation().Parameters((await widget.Describe()).Content));
    }
}
