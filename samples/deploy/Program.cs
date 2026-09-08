using Amazon.CDK;
using Deploy;

// The account and region come from the environment the way every CDK app's do, so `cdk deploy`
// with a profile set puts the widgets where that profile points.
var app = new App();

new WidgetStack(app, "LambdaWidgets", new StackProps {
    Env = new Amazon.CDK.Environment {
        Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
        Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
    }
});

app.Synth();
