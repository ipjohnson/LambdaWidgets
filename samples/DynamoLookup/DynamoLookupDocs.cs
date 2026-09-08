using DependencyModules.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace DynamoLookup;

/// <inheritdoc />
[SingletonService(As = typeof(IWidgetDocs))]
public class DynamoLookupDocs : IWidgetDocs {
    public string Markdown =>
        """
        ## DynamoDB lookup

        Looks items up by partition key, optionally narrowed by a sort key prefix, and pages through
        them. The table is set by the dashboard author rather than the viewer, so the function's role
        can be scoped to one table.

        ### Widget parameters

        Parameter | Description
        ---|---
        **table** | The table to query. A viewer cannot change it
        **limit** | Items per page. 10 by default

        ### Example parameters

        ```yaml
        table: orders
        limit: 10
        ```

        ### Permissions

        The function's role needs `dynamodb:Query` and `dynamodb:DescribeTable` on the configured
        table, and nothing else.
        """;
}
