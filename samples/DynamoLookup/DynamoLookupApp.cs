using Hardened.Amz.DynamoDbClient;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace DynamoLookup;

/// <summary>
/// A DynamoDB lookup, as a widget.
/// </summary>
/// <remarks>
/// The sample that proves paging without a session. A widget gets no cookies, no local storage and
/// no state between invocations, so everything the next page needs has to ride in the
/// <c>cwdb-action</c> the viewer clicks.
/// </remarks>
[HardenedModule]
[LambdaWidgetModule]
[DynamoDbModule]
[Enable<WidgetTemplates>]
public partial class DynamoLookupApp { }
