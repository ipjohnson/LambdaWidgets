using Xunit;

namespace LambdaWidgets.Dashboard.Tests;

/// <summary>
/// The console's documented defaults, as enum ordering. A parsed cwdb-action with the attribute
/// absent takes <c>default</c>, so these are the values that decide behaviour when an author
/// writes nothing.
/// </summary>
public class ConsoleContractTests {
    [Fact]
    public void Action_defaults_to_html() {
        Assert.Equal(ActionKind.Html, default(ActionKind));
    }

    [Fact]
    public void Display_defaults_to_widget() {
        Assert.Equal(Display.Widget, default(Display));
    }

    [Fact]
    public void Event_defaults_to_click() {
        Assert.Equal(ActionEvent.Click, default(ActionEvent));
    }
}
