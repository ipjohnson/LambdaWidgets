using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace Deploy;

/// <summary>What a widget needs beyond the function.</summary>
/// <param name="Name">
/// The function's name, which the console shows a dashboard author when they add a widget.
/// </param>
/// <param name="Asset">The published folder, which CI builds ahead of time on Linux.</param>
/// <param name="Policy">The API calls this widget makes, and no others.</param>
public sealed record WidgetFunction(string Name, string Asset, PolicyStatement[] Policy);

/// <summary>
/// The widgets, the dashboard that holds them, and who may use it.
/// </summary>
/// <remarks>
/// Plain CDK rather than a Hardened construct. <c>Hardened.Amz.Cdk</c> stopped at
/// <c>0.22.0-rc1000</c> and depends on the Lambda host that 0.30 replaced, so it cannot be taken
/// beside it, and there is no replacement yet — finding F-05. Writing the stack by hand also
/// documents what the eventual construct has to cover.
/// </remarks>
public sealed class WidgetStack : Stack {
    public WidgetStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props) {
        var functions = new[] {
            new WidgetFunction(
                "customWidgetLogsSearch",
                "../LogsSearch/bin/Release/net8.0/linux-arm64/publish",
                [
                    // Exactly the calls the widget makes. The role is the whole of what the tool
                    // may do, and it is the reason a widget is safer than the script it replaces:
                    // that script runs with whatever its author's credentials allow.
                    new PolicyStatement(new PolicyStatementProps {
                        Actions = ["logs:StartQuery", "logs:GetQueryResults", "logs:StopQuery",
                                   "logs:DescribeLogGroups"],
                        Resources = ["*"]
                    })
                ]),
            new WidgetFunction(
                "customWidgetEcho",
                "../Echo/bin/Release/net8.0/linux-arm64/publish",
                [])
        };

        var deployed = functions.ToDictionary(one => one.Name, one => Deploy(one));

        var dashboard = new CfnDashboard(this, "WidgetDashboard", new CfnDashboardProps {
            DashboardName = "lambda-widgets",
            DashboardBody = Body(deployed)
        });

        // One policy a viewer's role attaches, rather than a permission per person. Invoking the
        // function is not enough on its own - the console will not run a custom widget until the
        // viewer allows it, once or always - but without it the widget cannot run at all.
        var viewers = new ManagedPolicy(this, "WidgetViewers", new ManagedPolicyProps {
            ManagedPolicyName = "LambdaWidgetViewers",
            Statements = [
                new PolicyStatement(new PolicyStatementProps {
                    Actions = ["lambda:InvokeFunction"],
                    Resources = deployed.Values.Select(one => one.FunctionArn).ToArray()
                }),
                new PolicyStatement(new PolicyStatementProps {
                    Actions = ["cloudwatch:GetDashboard", "cloudwatch:ListDashboards"],
                    Resources = ["*"]
                })
            ]
        });

        new CfnOutput(this, "DashboardUrl", new CfnOutputProps {
            Value = $"https://{Region}.console.aws.amazon.com/cloudwatch/home?region={Region}" +
                    $"#dashboards:name={dashboard.DashboardName}",
            Description = "The dashboard holding the widgets"
        });

        new CfnOutput(this, "ViewerPolicy", new CfnOutputProps {
            Value = viewers.ManagedPolicyArn,
            Description = "Attach this to a role whose holders should be able to use the widgets"
        });
    }

    /// <remarks>
    /// <para>
    /// <c>provided.al2023</c> with the handler set to the assembly name, which is what a native
    /// binary deploys as: the runtime starts the executable and it talks the Runtime API itself.
    /// </para>
    /// <para>
    /// <b>The name carries the <c>customWidget</c> prefix AWS recommends.</b> Adding a custom widget
    /// means picking a function out of a list of every function in the account, and the prefix is
    /// the only signal a dashboard author has about which ones are meant for it.
    /// </para>
    /// </remarks>
    private Function Deploy(WidgetFunction widget) {
        var deployed = new Function(this, widget.Name, new FunctionProps {
            FunctionName = widget.Name,
            Runtime = Runtime.PROVIDED_AL2023,
            Architecture = Architecture.ARM_64,
            Handler = widget.Name,
            Code = Code.FromAsset(widget.Asset),
            // A widget answers while a viewer waits, and a Logs Insights query is polled inside the
            // invocation. Thirty seconds is longer than a page load should ever take and shorter
            // than the harness's proxy waits.
            Timeout = Duration.Seconds(30),
            MemorySize = 512
        });

        foreach (var statement in widget.Policy) {
            deployed.AddToRolePolicy(statement);
        }

        return deployed;
    }

    /// <summary>
    /// The dashboard body, which is the file the harness renders with the endpoints filled in.
    /// </summary>
    /// <remarks>
    /// <b>One artifact, developed locally and deployed unchanged.</b> The placeholder ARNs in
    /// <c>dashboard.json</c> carry the account and region of nowhere; every other property — the
    /// parameters, the sizes, <c>updateOn</c> — is what was developed against. A dashboard authored
    /// separately from the one the harness opens is two dashboards that will disagree.
    /// </remarks>
    internal static string Body(IReadOnlyDictionary<string, Function> deployed) {
        var body = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "dashboard.json"));

        foreach (var (name, function) in deployed) {
            body = body.Replace(
                $"arn:aws:lambda:us-east-1:000000000000:function:{name}",
                function.FunctionArn,
                StringComparison.Ordinal);
        }

        return body;
    }
}
