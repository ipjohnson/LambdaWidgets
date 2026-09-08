# Status

LambdaWidgets is being built in the open and nothing is on nuget.org yet. This page says what
exists, so no page here has to hedge.

| Item | State |
|---|---|
| 0. Repository scaffold, package pins, tool manifest, CI | Done |
| 0b. Uptake of Hardened 0.30: one version line, the adapter seam | Done |
| 1. `LambdaWidgets.Dashboard`, the interpreter | Not started |
| 2. `LambdaWidgets.Runtime`, the widget adapter, with the Echo sample | Not started |
| 3. Harness page, invoke proxy, test tool target, inspector | Not started |
| 4. `LambdaWidgets.Testing`, the click-through driver | Not started |
| 5. Logs Insights search sample | Not started |
| 6. DynamoDB lookup sample | Not started |
| 7. Deploy the search sample and check it in a real dashboard | Not started |
| 8. Native binaries, Docker image, dotnet tool, release workflow | Not started |
| 9. HTTP API for tests in other languages, and the linter | Not started |

## What is already true

The reference section is not a plan. Everything under
[The console](/reference/console-contract) was read from AWS's own documentation and its
`cloudwatch-custom-widgets-samples` repository, and it describes the console as it behaves today. It
is useful whether or not you ever use anything in this repository.

The guide describes the products being built. Each page says at the top what is not shipped yet.

## Why it exists

This repository is a proving ground for [Hardened](https://github.com/ipjohnson/Hardened.Framework),
a source-generated C# application framework. Custom widgets exercise both of its hosts at once: the
widget is a Lambda function, and the harness is a Kestrel web application. Everything awkward,
missing or broken that turns up while building on it is recorded in
[FINDINGS.md](https://github.com/ipjohnson/LambdaWidgets/blob/main/FINDINGS.md).

Adoption is a consequence, not the objective.
