# Development log

What building this was actually like, in order. Decisions and why, dead ends, corrections, and what
each probe cost to run.

`FINDINGS.md` is the other log and has a narrower job: one entry per thing that was awkward, missing
or broken in Hardened and the libraries under it. That log is the deliverable of the exercise. This
one is the account of the work, including the parts that were nobody's fault but our own.

Newest entry last. Written when it happens.

---

## 2026-09-06 — Scaffold, and two AOT questions answered before writing anything

The repository, the solution, CI with the CI flags, the VitePress site.

Two of the six day-one checks were answered with throwaway executables rather than by reading
release notes, and both came back cleaner than expected. The AWS SDK 3.7 line publishes under
`PublishAot` with zero `IL2xxx` or `IL3xxx`, so 4.x is not needed. AngleSharp 1.8.0 and Markdig
1.3.2 do too, which settled section 6 of the plan on both: no purpose-built tokenizer, and the
harness page does not have to render markdown in the browser.

Both probes did the jobs the code will actually do rather than merely linking the assemblies. That
distinction turned out to matter later.

Building `LambdaWidgets.SourceGenerator` cost three findings on its own: F-01, F-03 and F-04, all
about the source-shipped generator arrangement being undocumented or unguarded. Day-one check 2 was
the point of that project, and it passed.

## 2026-09-07 — Hardened 0.30 lands in the middle of the plan

The framework released `0.30.0-rc1000` before item 1 started. PR #305 is 746 files, +30,103 /
−3,920: four repositories consolidated into Hardened.Framework, `Hardened.Amz`'s source deleted, and
the AWS line rebuilt on a new host seam.

The seam is `IPayloadAdapter`, and reading it was the good surprise of the week. Its own doc
comments name this project's case twice without knowing about it: "a CloudWatch widget is web-shaped
despite arriving as a direct invoke", and, on `InvokeAdapter.OperationField`, "this is the same
mechanism a CloudWatch widget already uses through its own `route` field". Plan section 5.2 had
chosen `route` and marked the name undecided. It is decided by agreement rather than by argument.

The plan gets smaller. `LambdaWidgets.SourceGenerator` had one job, emitting an entry point, and
0.30 makes that a hand-written `Program.cs`. So the sharpest proof in the plan — a generator outside
Amz compiling the source-shipped package — is spent rather than ongoing. It ran, it passed, it
produced three findings, and keeping a generator that emits nothing to hold the question open would
be weight.

Written up as an uptake plan beside the implementation plan, rather than by editing the plan in
place, so the diff between the two is legible.

## 2026-09-07 — The DynamoDB packages, and being wrong in public

Reported that `Hardened.Amz.DynamoDbClient` was dropped at 0.30 with no successor and that the
Dynamo sample should fall back to the raw SDK. Wrong in two ways, and the correction came from being
pushed on it rather than from checking.

There were two packages, not one. `Hardened.Amz.DynamoDbClient.Testing` ships `[LocalDynamoDb]`,
which puts DynamoDB Local in a Testcontainers container behind the application's own
`IDynamoDbClientProvider` with no test method changing. Nothing at any version replaces that, and it
is the more valuable of the two.

And they were not lost. Both nuspecs name only `Hardened.Shared.Runtime` and
`Hardened.Shared.Testing`, which resolve up to 0.30, and neither package touches anything #305
rebuilt. The source was also recoverable: `cbdc53f1` imported Amz with its history before
`03e25e12` deleted 282 files. An earlier claim that the history was absent came from a shallow
clone, which is a good reminder that `git log` on a `--depth 1` clone answers confidently and
wrongly.

So the answer changed from "work around it" to "fix it upstream". Restored both as
`Hardened.Aws.DynamoDbClient` and `Hardened.Aws.DynamoDbClient.Testing` in Framework
[#307](https://github.com/ipjohnson/Hardened.Framework/pull/307), from `03e25e12^` rather than
retyped. All 24 of their tests pass against 0.30 unchanged, container tests included, which is the
evidence that the drop was a sweep rather than a decision. Finding F-06.

Three orphaned pins in the framework's own `Directory.Packages.props` — `AWSSDK.DynamoDBv2`,
`Amazon.CDK.Lib`, `Cdklabs.CdkMonitoringConstructs` — were the tell. Two of them have a consumer
again.

## 2026-09-07 — The uptake, and a green build that proved nothing

Item 0b: pins to 0.30 on one line, `LambdaWidgets.SourceGenerator` deleted with everything that
existed to compile it, findings F-05 to F-08 written.

Then the mistake worth recording, because it is a trap this repository is unusually exposed to.

Every `src` project is an empty stub until its item lands. `LambdaWidgets.Runtime` shipped in the
first uptake commit with **no generator reference at all** — not
`Hardened.Library.SourceGenerator`, not `DependencyModules.SourceGenerator` — while AGENTS.md
carried a new paragraph about which reference carries what. The build was green and CI was green,
because an analyzer with no source to read emits nothing. The failure would have surfaced at item 2,
in a different project, as a missing type.

The paragraph itself came from reading a comment in the framework's csproj and treating it as a
checked result. The comment was true and answered a different question. A four-minute probe was
available the whole time.

**Rule taken from it, now in AGENTS.md: verify a toolchain claim by compiling something that uses
it, never by building the stub.**

Day-one check 7 is that probe, and it answers the question the whole uptake rests on: a third-party
`IPayloadAdapter`, in a project outside the framework, on packages from nuget.org, is discovered and
run. All four assertions pass — the adapter resolves from the container, `Handles` is not asked when
it is the only one registered, a web-shaped request matches a `[Get]` route, and what
`WriteResponse` writes reaches the caller.

It cost three things reading had not turned up. Two generator packages are needed rather than one,
and getting it wrong fails at whoever applies the module rather than where the reference is missing.
A widget application needs `[HardenedWebModule]` as well, or the invocation fails with "This
function declares no handlers" — item 2 should compose that onto `[LambdaWidgetModule]` so an
author still writes two attributes. And a handler returning a bare `string` comes back
JSON-serialized, which is the reason section 5.3 routes HTML through a template rather than a
`string`.
