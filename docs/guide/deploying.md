# Deploying

::: warning Not shipped yet
Item 7 deploys the first sample for real. See [status](/status).
:::

A widget is an ordinary Lambda function. What makes it a widget is that a dashboard names it and a
viewer is allowed to invoke it.

## The function

Publish AOT for `provided.al2023` and deploy the executable as the handler. `samples/deploy` is a
C# CDK app on `Amazon.CDK.Lib` that does it.

There is no Hardened CDK package on the current line. `Hardened.Amz.Cdk` stopped at
`0.22.0-rc1000` and depends on the Lambda host that Hardened 0.30 replaced, so it cannot be taken
beside it. The stack here is plain CDK until a replacement ships.

Name the function with the prefix `customWidget`. AWS recommends it so a dashboard author can tell
which functions in an account are safe to add to a dashboard.

## The role

The function's execution role is the tool's authority. A widget that searches logs needs
`logs:StartQuery`, `logs:GetQueryResults`, `logs:StopQuery` and `logs:DescribeLogGroups`, and
nothing else.

Scope it as tightly as the tool allows. Anyone who can see the dashboard and is allowed to invoke
the function can do everything the role permits.

## The viewer

Two permissions, granted with a managed policy: `lambda:InvokeFunction` on the function, and
`cloudwatch:GetDashboard` on the dashboard.

A custom widget does not run until the viewer allows it, once or always, per widget or per
dashboard. The viewer can also deny it. That prompt is the console's, not yours.

## The audit trail

The event does not identify the viewer. Until that is shown to be wrong, the record of who did what
is the CloudTrail entry for the invoke, joined to the function's own log by request id.

Every invocation logs its route, the names of the fields and form keys it received, and the request
id. Values are not logged. That line is what makes the join possible.
