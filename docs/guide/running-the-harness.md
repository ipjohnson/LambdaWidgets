# Running the harness

::: warning Not shipped yet
Item 3 builds the harness page and item 8 builds the distribution. Nothing on this page runs today.
See [status](/status). What is written here is the shape it is being built to.
:::

`lambda-widgets` will ship three ways: a native binary per platform on the GitHub release, a Docker
image, and a dotnet tool for people who already have the SDK.

```bash
lambda-widgets --dashboard ./dashboard.json
```

It serves on `PORT`, defaulting to 5080.

## The dashboard file

The file is a real CloudWatch dashboard body, the same JSON that `PutDashboard` takes, holding
custom widget entries. Editing a widget in the page writes the same JSON back, so the file you
develop against is the file you deploy.

## Choosing where invokes go

The function name in a `cwdb-action` endpoint, the segment after `function:`, selects a target.
`--test-tool` is the default and maps every name to the AWS Lambda Test Tool on port 5050. The full
set is in [invoke targets](/reference/invoke-targets).

The page never calls a target directly. The test tool's Invoke API sets no CORS headers, so the
harness proxies every call server side, which also keeps credentials out of the browser.

## Starting a C# widget beside it

`dotnet run` is enough. A widget application's entry point asks `LambdaEmulator.StartIfLocal` for a
session, which starts the AWS Lambda Test Tool as a child process, or attaches to one already
listening, and points the bootstrap at it:

```bash
dotnet run --project samples/Echo
```

Reuse is what makes the debugger's stop button harmless. It kills the widget and not the tool, so
the next start finds the tool listening and carries on.

To drive the tool yourself instead, start it and set the address the service would have set:

```bash
dotnet tool restore
dotnet lambda-test-tool start --lambda-emulator-port 5050 --no-launch-window
```

```bash
AWS_LAMBDA_RUNTIME_API=localhost:5050/<AssemblyName> dotnet run --project samples/Echo
```
