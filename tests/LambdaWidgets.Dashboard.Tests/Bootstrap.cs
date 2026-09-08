using LambdaWidgets.Dashboard;

// The module under test, applied to the assembly. A [HardenedTest] resolves its parameters from
// the container these attributes build, so a test asks for IWidgetConsole and gets the composition
// an application would get rather than one the test assembled.
[assembly: DashboardModule]
