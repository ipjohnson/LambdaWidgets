# Findings

What was awkward, missing or broken in Hardened, RazorBlade, DependencyModules or ValidationModules
while building on them. Findings before 2026-09-07 name Hardened.Amz, which was a repository of its
own until Framework PR #305 consolidated it in. Written when it happens, not reconstructed later. This log
is the deliverable of the exercise.

A finding fixed upstream gets its PR link and the version that shipped it. A finding the maintainer
dismisses is deleted, not marked dropped.

---

## F-01  CSharpAuthor's version guard cannot check the version that works   2026-09-06

    Side: framework     Where it belongs: Hardened.Framework

`Hardened.SourceGenerator.targets` raises `HARDENED001` when the consuming project's CSharpAuthor
pin is older than 2.0.0. It compares with `System.Version.Parse`, so it deliberately skips any pin
that is not a plain dotted number:

```
<_HardenedCSharpAuthorComparable
    Condition="... Regex::IsMatch('$(_HardenedCSharpAuthorFound)', '^[0-9]+(\.[0-9]+){1,3}$')">true</...>
```

There is no CSharpAuthor 2.0.0. The newest is `2.0.0-preview1005`, which is what the Framework
builds its generators against and therefore the only version a consumer can correctly pin. It is a
prerelease, so the guard skips it and checks nothing.

The versions the guard *does* check are the ones nobody should use. `1.2.0` is the newest plain
version, and it correctly errors. So the guard fires only on pins that are already wrong for a
different reason, and stays silent on every pin that matters.

The comment in the targets file says as much: "until 2.0.0 final ships this floor speaks only to
consumers pinning a plain version like 1.2.0". That is accurate, and it means the guard is not
currently doing the job it was added for. Amz's `Directory.Packages.props` records the failure it
was meant to prevent: at 1.1.1006 the Framework's own `LinkGenerator` failed to compile in Amz for
want of `ComponentModifier.Sealed`.

**Fix:** compare prerelease versions too, or ship CSharpAuthor 2.0.0 final. The first is cheap and
does not wait on a release.

**Worked around here:** pinned `2.0.0-preview1005` and wrote down why, in
`Directory.Packages.props` and `AGENTS.md`. No longer carried: the 0.30 uptake deleted
`LambdaWidgets.SourceGenerator`, so nothing here compiles the source-shipped package any more. The
finding stands on its own, because it is about the package rather than about this repository.

    PR: none yet

---

## F-02  Hardened.Amz has not released against Framework 0.22.0-rc1000   2026-09-06

    Side: amz     Where it belongs: Hardened.Amz     Closed 2026-09-07

Hardened.Framework was on `0.22.0-rc1000` on nuget.org and the newest Hardened.Amz was
`0.21.0-rc1000`, with the work merged on Amz `main` and unpublished. That forced the Framework pin
back a line to match, and made the emulator change in #89 unavailable, so a widget sample could not
start the test tool from its own `Main`.

**Closed twice over.** Amz released `0.22.0-rc1000` on 2026-09-06, and Framework PR #305 then
consolidated Amz into Hardened.Framework, so there is one version line and nothing left to keep in
step. The by-hand test tool start is gone with it: `LambdaEmulator.StartIfLocal` starts the tool
from the entry point.

    Fixed in: Hardened.Framework 0.30.0-rc1000 (#305)

---

## F-03  RS2008 from source-shipped packages breaks any consumer that gates warnings   2026-09-06

    Side: framework     Where it belongs: Hardened.Framework, ValidationModules

`Hardened.SourceGenerator` and `ValidationModules.SourceGenerator.Impl` ship their `.cs` under
`src/` in the package and are compiled into the consuming analyzer assembly, which is the whole
point of the arrangement. Their `DiagnosticDescriptor`s come with them, so `RS2008`, "enable
analyzer release tracking", is reported against the *consumer* for rules the consumer does not own.

The first CI-flags build of `LambdaWidgets.SourceGenerator` failed with 68 errors, every one of them
in a file under `~/.nuget/packages`:

```
hardened.sourcegenerator/0.21.0-rc1000/src/Hardened.SourceGenerator/Validation/HandlerValidationDiagnostics.cs(58,9):
  error RS2008: Enable analyzer release tracking for the analyzer project containing rule 'HRDV005'
validationmodules.sourcegenerator.impl/1.0.0/src/ValidationModules.SourceGenerator.Impl/ValidationDiagnostics.cs(53,9):
  error RS2008: ... rule 'VM1001'
```

Nothing in a consuming repository can add release tracking for `HRDV005` or `VM1001`. The only
available answers are to suppress the rule or to turn off `EnforceExtendedAnalyzerRules`, which also
turns off the checks that do apply to the consumer's own generator.

Hardened.Amz hits this and suppresses it in its own `Directory.Build.props`, with a comment saying
exactly why. That is the tell: every consumer of these packages will write the same suppression, so
it belongs in the packages.

**Fix:** ship `AnalyzerReleases.Shipped.md` inside the source-only packages alongside the `.cs`, or
set `<NoWarn>$(NoWarn);RS2008</NoWarn>` in `Hardened.SourceGenerator.targets` under the same
`PackageHardenedIncludeSource` condition that includes the source.

**Worked around here:** `NoWarn` for `RS2008` in `Directory.Build.props`, scoped to
`*SourceGenerator*` projects. Removed in the 0.30 uptake along with the generator project. The
finding stands: every consumer who compiles these packages in will write the same suppression.

    PR: none yet

---

## F-04  The source-shipped generator arrangement is undiscoverable   2026-09-06

    Side: framework     Where it belongs: Hardened.Framework

Compiling `Hardened.SourceGenerator` into a generator project requires four MSBuild properties and
three package references with particular asset settings:

```xml
<PackageHardenedIncludeSource>true</PackageHardenedIncludeSource>
<PackageCSharpAuthorIncludeSource>true</PackageCSharpAuthorIncludeSource>
<PackageCSharpAuthorIncludeRoslyn>true</PackageCSharpAuthorIncludeRoslyn>
<PackageValidationModulesIncludeSource>true</PackageValidationModulesIncludeSource>
```

None of it is in the package README, and the package's own `Hardened.SourceGenerator.targets`
mentions only the first. The remaining three, and the fact that `ValidationModules.SourceGenerator.Impl`
has to be referenced at all, were recovered by reading
`src/SourceGenerators/Lambda/Web/Hardened.Amz.Web.Lambda.SourceGenerator.csproj` in Hardened.Amz.

A consumer who sets `PackageHardenedIncludeSource` alone gets unresolved-type errors inside the
framework's own source files, naming APIs they never wrote. That is the failure mode the
`HARDENED001` guard was added to improve, and it is the same failure for a different missing switch.

**Fix:** document the set in the package README, or collapse it. `PackageHardenedIncludeSource` could
imply the other three, since Hardened's source does not compile without them.

**No longer carried here.** The 0.30 uptake deleted the generator project. Day-one check 2 had
already answered the question it was written to answer, and the arrangement is unchanged upstream.

    PR: none yet

---

## F-05  No CDK package on the 0.30 line   2026-09-07

    Side: framework     Where it belongs: Hardened.Framework

`Hardened.Amz.Cdk` `0.22.0-rc1000` is the newest, and there is no `Hardened.Aws.Cdk`. A consumer on
0.30 has no supported way to deploy what it builds.

It cannot be taken beside 0.30 either. Its nuspec depends on `Hardened.Amz.Shared.Lambda.Runtime`
`0.22.0-rc1000`, so referencing it puts the old Lambda host in the same build as the new one, with
`Hardened.Requests.Runtime` resolving up to 0.30 underneath an assembly compiled against 0.22. That
is the version-skew failure F-02 was about, in the other direction.

PR #305 names the replacement and its state in one line: "Nothing generates infrastructure yet -
`AddEventSourcesFrom(HardenedRoutes.All)` on the CDK side is still to come."

**Fix:** an `Hardened.Aws.Cdk` on the line, or a note on `/aws/cdk` saying what a 0.30 consumer
should do instead.

**Worked around here:** `samples/deploy` is written on `Amazon.CDK.Lib` directly. The function on
`provided.al2023` with an executable handler, a role, a dashboard and a viewer policy is a page of
plain CDK, and writing it by hand documents what the eventual construct has to cover.

    PR: none yet

---

## F-06  The DynamoDB client and its test harness were dropped without a successor or a note   2026-09-07

    Side: framework     Where it belongs: Hardened.Framework     Fixed 2026-09-07

Two packages, `Hardened.Amz.DynamoDbClient` and `Hardened.Amz.DynamoDbClient.Testing`, frozen at
`0.22.0-rc1000` and neither rebuilt at 0.30. `Hardened.Aws.Lambda.DynamoDb` is the Streams adapter
rather than a client, and the shared name makes the gap easy to miss reading the package list.

The testing one was the real loss. `[LocalDynamoDb]` puts DynamoDB Local in a container behind the
application's own `IDynamoDbClientProvider`, with no test method changing, and nothing at any
version replaced it.

Neither package touched anything #305 rebuilt, which is what made the drop look incidental rather
than decided. They bind `Hardened.Shared.Runtime` and `Hardened.Shared.Testing` and nothing on the
host seam. The release notes do not mention them, and the pins they needed were still in the
framework's own `Directory.Packages.props` with no project referencing them, beside `Amazon.CDK.Lib`
and `Cdklabs.CdkMonitoringConstructs`, which are still orphaned there.

**Fixed upstream.** Restored as `Hardened.Aws.DynamoDbClient` and `Hardened.Aws.DynamoDbClient.Testing`
from `03e25e12^` rather than rewritten, renamed and otherwise unedited. All 24 of their tests pass
against 0.30 with the container tests included, which is the evidence that nothing about them needed
rewriting and that the drop was a sweep.

**And the interim works, verified rather than assumed   2026-09-08.** #307 is merged and its line
has not shipped, so `samples/DynamoLookup` takes `Hardened.Amz.DynamoDbClient` at `0.22.0-rc1000`
alongside the 0.30 pins. A throwaway consumer resolved `IDynamoDbClientProvider` from a container
built by `[DynamoDbModule]` and got a working client; the sample then published `PublishAot` with
zero `IL2xxx` or `IL3xxx`, carrying the frozen package and the 0.30 line in one binary. That is the
claim this entry has rested on since it was written, now tested from outside.

    PR: Hardened.Framework #307, open. Ships on the line after 0.30.0-rc1000.

---

## F-07  InvokeAdapter.OperationField is declared and never read   2026-09-07

    Side: framework     Where it belongs: Hardened.Framework

`Hardened.Aws.Lambda.Invoke` declares:

```csharp
public const string OperationField = "operation";
```

with the clearest description in the codebase of how a multi-operation direct-invoke function
selects a handler, citing the CloudWatch widget's own `route` field as the precedent. Nothing
references it. `InvokeAdapter.CreateRequest` routes every payload to `"/" + context.FunctionName`
unconditionally, so a direct-invoke function with several operations cannot address them.

Either the field is read or the constant documents something the adapter does not do. The comment
reads as a description of behaviour, which is what makes it worth an entry: it cost a reading of the
adapter to find out otherwise.

**Fix:** read the field in `CreateRequest`, or move the paragraph to wherever the mechanism is
actually meant to live.

**Consequence here:** none beyond the confusion. The widget adapter implements `route` itself, which
was always the plan.

    PR: none yet

---

## F-08  The documentation site describes the packages the release replaced, as current   2026-09-07

    Side: framework     Where it belongs: Hardened.Framework

Not one stale page. The 0.30 site's AWS section is written against the deleted line throughout, and
every page below is in the nav:

- `/aws/` opens with `using Hardened.Amz.Web.Lambda.Runtime.DependencyInjection;` and
  `[LambdaWebModule]`, and its "Where things are" table links to `github.com/ipjohnson/Hardened.Amz`
  paths that no longer exist.
- `/reference/attributes` lists `[LambdaWebModule]`, `[LambdaFunctionModule]`, `[SqsLambda]`,
  `[DynamoStreamLambda]`, `[HardenedCdk]` and more, each against its `Hardened.Amz.*` package.
- `/reference/packages` inventories the Amz line and lists none of the eleven `Hardened.Aws.Lambda.*`
  packages that replaced it.

`/reference/repository` says the Amz line "stays on nuget.org at 0.22.0-rc1000 ... and the AWS pages
describe it as released", so some of this is deliberate. It does not read that way from any of the
pages: none says which version it describes, and `/aws/` is where an AWS reader lands first.

**Fix:** a version banner on the AWS section, and the new package names in the examples on the pages
that have successors.

**Partly fixed:** Framework #307 moved the two DynamoDB pages onto the new names as part of
restoring those packages. The Lambda pages are untouched.

    PR: none yet

---

## F-09  A transport with a query string and no cookies has no conformance profile   2026-09-07

    Side: framework     Where it belongs: Hardened.Framework

`IPayloadAdapter` says a CloudWatch widget is web-shaped, and the shape "decides which conformance
profile the adapter enrols in". A widget cannot enrol in the web one.

`ExecutionRequestConformanceTests` adds three assertions over the payload profile:
`QueryStringIsSurfaced`, `QueryStringValuesArriveDecoded` and `CookiesAreSurfaced`. A widget
satisfies the first two — its merged parameters are exactly a query string, and it is the only
channel a `[FromQueryString]` parameter has. It cannot satisfy the third: the console sends no
header of any kind, and a direct invocation's one header-like channel is the SDK caller's client
context, which the console does not set.

So the split is two profiles for three cases. The choices are to enrol in the payload profile and
lose the two query assertions that do apply, or to give the request a cookie list it never carries
so a suite passes. Neither is right, and the second is worse: a conformance suite that can be
satisfied by inventing a channel is asserting less than it looks like it is.

**Fix:** split `CookiesAreSurfaced` out the way the query assertions were already split out, so a
transport enrols in what it can answer. The comment on `ExecutionRequestConformanceTests` makes the
same argument for the existing split — "a skipped test is one this repository fails CI on" — and
this is the same problem one case further along.

**Worked around here:** enrolled in `PayloadExecutionRequestConformanceTests`, with the two web
assertions that apply written out in `WidgetRequestConformanceTests` rather than lost.

    PR: none yet

---

## F-10  A transport whose GET carries a body has no way to say so   2026-09-07

    Side: framework     Where it belongs: Hardened.Framework

`HRDR010` refuses a complex parameter on a `[Get]` handler:

    Parameter 'request' of 'Pages.Search' is read from the request body, and a GET carries none, so
    a request that sends no body is refused before the handler runs and the published document
    gives the operation a body it should not have.

Both halves are true of HTTP and neither is true of a widget. Its `GET` is a scheme label rather
than a method — the console sends no method at all, and the verb is what puts a widget's pages in
the generated `Links` and `Routes` types. There is no client that could omit a body, because the
adapter writes it. And a widget publishes no OpenAPI document.

The diagnostic offers three ways out: `[FromQueryString]`, `[FromServices]`, or suppression. The
first is per-parameter and gives up the request object the whole design is written around; the
third silences a rule rather than answering it.

**Not a defect, and worth an entry anyway.** The rule is right for every transport the framework
ships, and a non-HTTP transport reusing the web verbs is a case it has not met. The gap is that
there is no way for a transport to declare that its GET does carry a body, so every such transport
either suppresses the rule or works around it.

**Worked around here, and it turned out to be the better design.** `[FromWidget]` implements
`ICustomBindingAttribute`, which is a source of its own, so `HRDR010` does not apply. The attribute
then says at the handler what is happening rather than leaving it to a suppression in a props file,
and it binds through `ISerializationLocatorService` rather than reflecting over the parameter type —
so it stays AOT-safe and inherits `AllowReadingFromString`, which is what makes a form field's
`"20"` arrive as an `int`.

    PR: none yet

---

# Day-one checks

Section 11 of the plan. Each is recorded here with what was observed.

## 1. The AWS SDK under AOT   Answered 2026-09-06

**The 3.7 line publishes clean. Zero trim and AOT warnings.**

A throwaway `net8.0` executable referencing `AWSSDK.CloudWatchLogs` 3.7.509.3,
`AWSSDK.DynamoDBv2` 3.7.513.4 and `AWSSDK.Lambda` 3.7.511.24, constructing all three clients and
touching `StartQueryRequest`, `GetQueryResultsRequest`, `QueryRequest` and `InvokeRequest`,
published with `PublishAot` and `TrimmerSingleWarn=false` for `osx-arm64`. ILC emitted no `IL2xxx`
or `IL3xxx` at all. The binary is 11.3 MB and runs.

So the pins stay on 3.7 and 4.x is not needed.

**Rechecked for real 2026-09-08, and it holds.** The probe covered construction and request types
rather than a working widget, and said so. `samples/LogsSearch` publishes `PublishAot` for
`osx-arm64` with `TrimmerSingleWarn=false` and emits **no `IL2xxx` or `IL3xxx` at all** — a 17 MB
binary carrying the Hardened runtime, the widget adapter, RazorBlade views and
`AWSSDK.CloudWatchLogs` calling `StartQuery`, `GetQueryResults` and `StopQuery` with their responses
deserialized. Nothing in the stack needs a trim hint.

## 2. The host seam   Answered 2026-09-06

**A generator outside Amz can take `Hardened.SourceGenerator` from nuget.org and compile it in.**
`LambdaWidgets.SourceGenerator` does it, builds clean under `-p:ContinuousIntegrationBuild=true`,
and produces a 1.07 MB assembly, which is the ~150 compiled-in source files plus CSharpAuthor and
ValidationModules.Impl. This is the validation the source-shipped package has never had, and it
passes.

What it cost: findings F-01, F-03 and F-04. All three are about the arrangement being undocumented
or unguarded, none about the code.

**Superseded by the 0.30 uptake, answer intact.** `LambdaWidgets.SourceGenerator` is deleted:
Hardened 0.30 makes the entry point a hand-written `Program.cs`, so there is nothing for a generator
here to emit. The question this check asked was answered before that, and F-01, F-03 and F-04 are
what it cost.

The model to read for item 2 is now in Hardened.Framework rather than Amz: `IPayloadAdapter`,
`ApiGatewayAdapter` for the request half, `InvokeAdapter` for the response half, and
`LambdaInvocationHandler` for the loop that drives them.

## 3. The probe

    Open. Needs an AWS account. Deploy AWS's Echo widget from the samples page, feed HTML through
    its echo parameter, and record what the console keeps, strips and sends.

Everything it settles is marked **unverified** in `docs/reference/` until it runs.

What it has to answer has grown, and the CSS half of it is now the sharpest part.

- **Does one widget's CSS reach another?** Deploy Echo twice on one dashboard. Give A the parameter
  `<style>td{background:#f00}</style><table><tr><td>A</td></tr></table>` and B just
  `<table><tr><td>B</td></tr></table>`. If B's cell is red there is no isolation. This is the one
  that decides whether the linter's unscoped-selector rule is advice or a hard error, and whether
  build-time scoping is worth building.
- **If it is isolated, by what?** Open devtools on that dashboard: a shadow root shows in the
  elements panel and settles the mechanism outright. Shadow DOM would also explain why
  `cwdb-no-default-styles` acts per widget rather than per dashboard.
- **Which side of the boundary is `cwdb-theme-dark` on?** Have A emit
  `.cwdb-theme-dark td{background:#0f0}` and view the dashboard in dark mode. The class is confirmed
  real; whether a widget's own stylesheet can select on it as an ancestor is not.
- **Does a `cwdb-action` bind inside an `<svg>`?** Our interpreter binds the previous element
  sibling wherever it is. If the console does not, clicking a column does nothing — which is why
  every chart also puts the same links in the table beneath it.

`<style>` itself is no longer in question: AWS documents that a stylesheet can be included anywhere
in the returned HTML, and that `:hover` works.
- **What does `widgetContext.width`/`height` carry?** The interpreter passes the dashboard body's
  grid units straight through as pixels, which cannot both be right. A chart sizes itself from the
  `viewBox` and does not care, but the harness's fidelity does.

## 4. The test tool's ARN   Answered 2026-09-08

**A fixed placeholder, the same for every function.** Run under the AWS Lambda Test Tool,
`ILambdaContext.InvokedFunctionArn` is

    arn:aws:lambda:us-west-2:123412341234:function:Function

regardless of the assembly name, the `--function` argument or the invoke path. The Razor helpers
write that string into every `cwdb-action` endpoint, and a click posts back to it.

Two consequences. The harness cannot route by function name under the test tool, so
`--function name=url` works against a runtime interface emulator or SAM and not against this
target — `InvokeTargets.TestTool` maps every widget onto the one port instead. And a widget under
the test tool cannot tell what it is deployed as, so anything reading the ARN for its own account or
region gets the placeholder's.

## 5. The parsers under AOT   Answered 2026-09-06

**Both publish clean. Zero trim and AOT warnings. Take AngleSharp and Markdig.**

A throwaway referencing AngleSharp 1.8.0 and Markdig 1.3.2, published the same way, produced no
`IL2xxx` or `IL3xxx` and a 7.45 MB binary. The probe did the three jobs the interpreter actually
needs rather than just linking the assemblies: it found a `cwdb-action` and its
`PreviousElementSibling`, enumerated `input[name], textarea[name], select[name]`, and rendered
describe markdown with a fenced `yaml` block.

That settles section 6. No purpose-built tokenizer, and the harness page does not have to render
markdown in the browser.

## 6. Amz 0.22.0   Answered 2026-09-06, closed 2026-09-07

**It shipped, and then the question stopped existing.** Amz released `0.22.0-rc1000` on 2026-09-06.
Framework PR #305 then consolidated Amz into Hardened.Framework, so there is one version line. See
F-02.

## 7. A third-party IPayloadAdapter is discovered and run   Answered 2026-09-07

**It works, outside the framework, from packages on nuget.org. Three things it cost.**

A throwaway `net8.0` executable on the 0.30 packages: a `WidgetAdapter : IPayloadAdapter`, a
`[DependencyModule] [LambdaRuntimeModule]` module registering it, an application applying that
module, and a `[Get("/")]` handler. A widget-shaped payload was fed to
`LambdaInvocationHandler.Invoke` with an `ILambdaContext` of its own. All four assertions pass:

    PASS  adapter resolved from the container
    PASS  Handles not called for a single adapter
    PASS  route matched, handler ran
    PASS  WriteResponse output reaches the caller

Every framework type the adapter needs is public and reachable: `IPayloadAdapter`, `LambdaPayload`,
`LambdaPayloadRequest`, `LambdaPayloadResponse`, `HostFailurePolicy`, `IExecutionContext`,
`LambdaInvocationHandler` and `[LambdaRuntimeModule]`. The seam section 5 of the plan is built on
holds.

Three things the probe settled that reading had not:

1. **Two generator packages, not one.** `Hardened.Library.SourceGenerator` emits
   `PopulateServiceCollection` and does not emit a module's attribute.
   `DependencyModules.SourceGenerator` 1.3.1 does. With only the first, the build fails at whoever
   applies the module with `CS0616 'LambdaWidgetModule' is not an attribute class`, which names
   neither the missing package nor the project missing it.

2. **A widget application needs `[HardenedWebModule]`.** Plan section 5.1 shows `[HardenedModule]`
   and `[LambdaWidgetModule]` alone, and that registers no route table: the invocation fails with
   "This function declares no handlers." The framework's own `ApiGatewayTestApp` carries
   `[HardenedWebModule]` for exactly this. Item 2 should compose it onto `[LambdaWidgetModule]`, the
   way `LambdaRuntimeModule` composes `[HardenedRequestModule]`, so a widget application still
   writes two attributes.

3. **A handler returning a bare `string` is JSON-serialized on the way out.** The probe's answer was
   `"\u003Ch1\u003Ehello\u003C/h1\u003E"` rather than the HTML. Section 5.3 already routes an
   HTML response through a template or `IWidgetHtml`; this is the reason it has to.

A throwaway application in a project outside the framework, with a module registering one
`IPayloadAdapter` that returns a fixed string, invoked through `HardenedLambdaBootstrap` under the
test tool. What it has to show: the adapter is resolved from the container, `Handles` is not called
when it is the only one registered, what `WriteResponse` writes reaches the caller, and a web-shaped
request built by hand matches a `[Get]` route.

Everything in section 5 of the plan assumes all four.
