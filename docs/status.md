# Status

LambdaWidgets is on nuget.org at `0.1.0-rc1000`, a prerelease. This page says what exists, so no
page here has to hedge.

```bash
dotnet tool install --global LambdaWidgets.Harness --prerelease
dotnet new install LambdaWidgets.Templates::0.1.0-rc1000
dotnet add package LambdaWidgets.Runtime --prerelease
```

A prerelease because everything here depends on Hardened.Framework `0.30.0-rc1000`, which is one
itself. A stable version of this would be claiming more than the stack under it has.

| Item | State |
|---|---|
| 0. Repository scaffold, package pins, tool manifest, CI | Done |
| 0b. Uptake of Hardened 0.30: one version line, the adapter seam | Done |
| 1. `LambdaWidgets.Dashboard`, the interpreter | Done, less what the probe settles |
| 2. `LambdaWidgets.Runtime`, the widget adapter, with the Echo sample | Done |
| 3. Harness page, invoke proxy, test tool target, inspector | Done |
| 4. `LambdaWidgets.Testing`, the click-through driver | Done |
| 5. Logs Insights search sample | Done |
| 6. DynamoDB lookup sample | Done |
| 7. Deploy the search sample and check it in a real dashboard | Stack written; the dashboard check needs an account |
| 8. Native binaries, Docker image, dotnet tool, release workflow | Done, untagged |
| 9. HTTP API for tests in other languages, and the linter | Done |
| 10. `LambdaWidgets.Charts`, SVG charts with a hover layer | Done |
| 11. `dotnet new` templates for the three widget shapes | Done |
| 12. First release: six packages, five binaries, an image | Done |

## What is already true

The reference section is not a plan. Everything under
[The console](/reference/console-contract) was read from AWS's own documentation and its
`cloudwatch-custom-widgets-samples` repository, and it describes the console as it behaves today. It
is useful whether or not you ever use anything in this repository.

The guide describes what is released. The few pages with something still unproven say so at the
top: [deploying](/guide/deploying) has not been run against a real account, the console's handling
of a chart's `style` block and its `cwdb-action` elements is [unverified](/guide/charting-data), the
pixel size the console reports for a widget is [estimated](/guide/charting-data) from the grid units
a dashboard body holds, and `--aws` is the one [invoke target](/reference/invoke-targets) not built.

## Why it exists

This repository is a proving ground for [Hardened](https://github.com/ipjohnson/Hardened.Framework),
a source-generated C# application framework. Custom widgets exercise both of its hosts at once: the
widget is a Lambda function, and the harness is a Kestrel web application. Everything awkward,
missing or broken that turns up while building on it is recorded in
[FINDINGS.md](https://github.com/ipjohnson/LambdaWidgets/blob/main/FINDINGS.md).

Adoption is a consequence, not the objective.
