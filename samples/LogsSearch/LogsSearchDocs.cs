using DependencyModules.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace LogsSearch;

/// <inheritdoc />
[SingletonService(As = typeof(IWidgetDocs))]
public class LogsSearchDocs : IWidgetDocs {
    public string Markdown =>
        """
        ## Logs Insights search

        Runs a CloudWatch Logs Insights query over the dashboard's time range and shows the rows.
        The range follows the dashboard's picker, including a zoom on a graph beside it, so there is
        no time field on the form.

        ### Widget parameters

        Parameter | Description
        ---|---
        **logGroups** | Comma separated log group names to search
        **query** | The starting query. A viewer can edit it; Reset puts this back
        **limit** | Rows to return. 20 by default

        ### Example parameters

        ```yaml
        logGroups: /aws/lambda/orders,/aws/lambda/checkout
        query: fields @timestamp, @message | sort @timestamp desc | limit 20
        limit: 20
        ```

        ### Permissions

        The function's role needs `logs:StartQuery`, `logs:GetQueryResults`, `logs:StopQuery` and
        `logs:DescribeLogGroups` on the log groups it searches, and nothing else.
        """;
}
