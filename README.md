# LambdaWidgets

A C# framework for CloudWatch custom widget Lambdas, and a language-agnostic local dashboard for
developing them.

**Documentation: <https://ipjohnson.github.io/LambdaWidgets>**

> Nothing is on nuget.org yet, and the first release is what puts it there. See
> [status](https://ipjohnson.github.io/LambdaWidgets/status) for what exists. The
> [console contract](https://ipjohnson.github.io/LambdaWidgets/reference/console-contract)
> reference describes CloudWatch as it behaves today and is useful on its own.

## What a custom widget is

A dashboard widget backed by a Lambda function. The dashboard invokes the function, the function
returns HTML, and a `cwdb-action` element in that HTML re-invokes the function with parameters and
the widget's form fields when the user clicks. The response replaces the widget or opens a popup.

One widget is therefore a whole server-rendered application inside the console: a landing page,
links to tools, a form, a result. The function's execution role decides what the tool may do. IAM on
the dashboard and on the function decides who may use it.

Teams inside AWS build operational tools this way. Outside AWS the feature is close to unused. The
official samples are Python and JavaScript scripts, nobody has written a framework for it, and
nobody has a local development loop for it in any language.

## The three products

**The framework.** `LambdaWidgets.Runtime` and `LambdaWidgets.Testing`. A widget host for a Hardened
application, Razor helpers that emit `cwdb-action` from the generated links, form binding, describe,
and a click-through test driver. For C# authors.

**The harness.** `lambda-widgets`, a console-shaped dashboard that runs on your machine, interprets
clicks and forms, and invokes the widget through any Lambda Invoke API endpoint. Native binaries, a
Docker image, and a dotnet tool. For every widget author, any language.

**The samples.** Echo, a Logs Insights search, and a DynamoDB lookup, built on the framework and run
in the harness. They are the integration tests of both products and the demo a reader runs with no
AWS account.

## Why it exists

This is a proving ground for [Hardened](https://github.com/ipjohnson/Hardened.Framework), a
source-generated C# application framework. Custom widgets exercise both of its hosts at once: the
widget is a Lambda function and the harness is a Kestrel web application, so the proof runs on both
sides in one repository.

Everything awkward, missing or broken that turns up while building on it is recorded in
[FINDINGS.md](FINDINGS.md). That log is the deliverable. Adoption is a consequence, not the
objective.

## Running the harness

A single native binary with nothing to install:

```bash
lambda-widgets --dashboard dashboard.json
```

It opens on <http://localhost:5080> with your dashboard's widgets on it, invokes them through
whatever is serving them, and shows you the event it sent, what the console's sanitizer would have
removed and how long the invoke took. None of that is visible on a real dashboard.

Get it as a native binary from the [releases](https://github.com/ipjohnson/LambdaWidgets/releases),
as a container, or as a dotnet tool if you already have the SDK:

```bash
docker run --rm -p 5080:5080 -v "$PWD/dashboard.json:/dashboard.json" ghcr.io/ipjohnson/lambda-widgets
dotnet tool install --global LambdaWidgets.Harness
```

Your widget can be written in anything. The harness talks the Lambda Invoke API, so it drives the
AWS Lambda Test Tool, a runtime interface emulator, `sam local`, or a deployed function.

## Building

```bash
dotnet tool restore
dotnet build LambdaWidgets.slnx
dotnet test  LambdaWidgets.slnx
```

Before opening a pull request, build the way CI does. `ContinuousIntegrationBuild` sets
`TreatWarningsAsErrors`, so a build that is green locally can still fail CI on a warning:

```bash
dotnet build LambdaWidgets.slnx -c Release -p:ContinuousIntegrationBuild=true
```

The documentation site:

```bash
npm install
npm run docs:dev
```

See [AGENTS.md](AGENTS.md) for the invariants and the traps.

## Licence

MIT.
