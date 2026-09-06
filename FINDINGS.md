# Findings

What was awkward, missing or broken in Hardened, Hardened.Amz, RazorBlade, DependencyModules or
ValidationModules while building on them. Written when it happens, not reconstructed later. This log
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
`Directory.Packages.props` and `AGENTS.md`.

    PR: none yet

---

## F-02  Hardened.Amz has not released against Framework 0.22.0-rc1000   2026-09-06

    Side: amz     Where it belongs: Hardened.Amz

Hardened.Framework is on `0.22.0-rc1000` on nuget.org. The newest Hardened.Amz there is
`0.21.0-rc1000`.

Amz `main` has merged the work already: `254ad508` takes the Framework 0.22.0 release (#90),
`7c7e9534` starts the AWS Lambda Test Tool from the generated `Main` and drops the web harness
(#89), and `ae7aab1e` sets the release's expected package count to 14 (#91). None of it is
published.

Two consequences for a new consumer. The Framework has to be pinned back to `0.21.0-rc1000` to match
Amz, because Amz `0.21.0-rc1000` was compiled against it and a generator emitting calls against a
contract from a different line is a compile error in generated code. And the emulator change in #89
is unavailable, so a widget sample cannot start the test tool from its own `Main`; the tool has to
be started by hand.

**Fix:** release Amz `0.22.0-rc1000`.

**Worked around here:** both pinned at `0.21.0-rc1000`, and the by-hand test tool start documented
in `docs/guide/running-the-harness.md`.

    PR: none yet

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
`*SourceGenerator*` projects.

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

So the pins stay on 3.7 and 4.x is not needed. Recheck when the samples call these for real: this
probe covers construction and request types, not response deserialization.

## 2. The host seam   Answered 2026-09-06

**A generator outside Amz can take `Hardened.SourceGenerator` from nuget.org and compile it in.**
`LambdaWidgets.SourceGenerator` does it, builds clean under `-p:ContinuousIntegrationBuild=true`,
and produces a 1.07 MB assembly, which is the ~150 compiled-in source files plus CSharpAuthor and
ValidationModules.Impl. This is the validation the source-shipped package has never had, and it
passes.

What it cost: findings F-01, F-03 and F-04. All three are about the arrangement being undocumented
or unguarded, none about the code.

Still to read for item 2: `WebLambdaSourceGenerator`, `ApplicationFileWriter`,
`ApiGatewayEventProcessor`, `ApiGatewayV2ExecutionRequest` and `LambdaWebHost` in Amz, as the model
for mapping a widget event onto an `IExecutionRequest`.

## 3. The probe

    Open. Needs an AWS account. Deploy AWS's Echo widget from the samples page, feed HTML through
    its echo parameter, and record what the console keeps, strips and sends.

Everything it settles is marked **unverified** in `docs/reference/` until it runs.

## 4. The test tool's ARN

    Open. Run Echo under the test tool and read what Lambda-Runtime-Invoked-Function-Arn carries,
    so the harness's name mapping and the helper's endpoint agree locally.

## 5. The parsers under AOT   Answered 2026-09-06

**Both publish clean. Zero trim and AOT warnings. Take AngleSharp and Markdig.**

A throwaway referencing AngleSharp 1.8.0 and Markdig 1.3.2, published the same way, produced no
`IL2xxx` or `IL3xxx` and a 7.45 MB binary. The probe did the three jobs the interpreter actually
needs rather than just linking the assemblies: it found a `cwdb-action` and its
`PreviousElementSibling`, enumerated `input[name], textarea[name], select[name]`, and rendered
describe markdown with a fenced `yaml` block.

That settles section 6. No purpose-built tokenizer, and the harness page does not have to render
markdown in the browser.

## 6. Amz 0.22.0   Answered 2026-09-06

**It has not shipped.** See F-02. The by-hand test tool start is the path until it does.
