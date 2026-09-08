using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Charts;
using LambdaWidgets.Runtime;

namespace Graph;

/// <summary>
/// Charting a data source, as a widget.
/// </summary>
/// <remarks>
/// The sample stands in for a query rather than running one, so it can be opened with no AWS
/// account. Where <see cref="Metrics"/> makes numbers up, a real widget calls Logs Insights,
/// DynamoDB or CloudWatch and shapes the answer into the same <c>Series</c>.
/// </remarks>
[HardenedModule]
[LambdaWidgetModule]
[ChartsModule]
[Enable<ChartTemplates>]
public partial class GraphApp { }
