namespace LambdaWidgets.Dashboard;

/// <summary>
/// What a <c>cwdb-action</c> does when it fires.
/// </summary>
public enum ActionKind {
    /// <summary>Display the element's HTML content. The console's default.</summary>
    Html,

    /// <summary>Invoke the Lambda named by <c>endpoint</c> with the element's JSON content as parameters.</summary>
    Call
}

/// <summary>
/// Where the result of an action is shown.
/// </summary>
public enum Display {
    /// <summary>Replace the widget's content. The console's default.</summary>
    Widget,

    /// <summary>Show the result in a modal.</summary>
    Popup
}

/// <summary>
/// The DOM event that fires an action.
/// </summary>
public enum ActionEvent {
    /// <summary>The console's default.</summary>
    Click,
    DblClick,

    /// <summary>Valid only with <see cref="ActionKind.Html"/>.</summary>
    MouseEnter
}

/// <summary>
/// The dashboard's colour scheme, which arrives as <c>widgetContext.theme</c>.
/// </summary>
public enum Theme {
    Light,
    Dark
}
