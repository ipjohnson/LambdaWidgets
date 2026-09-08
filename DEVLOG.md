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

## 2026-09-07 — The interpreter, and being told how to test

Item 1. `LambdaWidgets.Dashboard` is the console's rules: what an invocation carries, what a click
sends, what survives rendering, what a form field contributes.

Two corrections landed on this before a line of it was right, and both were the same mistake in
different clothes.

**It is a module, not a bag of static classes.** The first draft was `static class Actions`,
`static class Sanitizer` and so on, which is not how a Hardened library is built: every shipped one
—  `Hardened.Web.StaticContent`, `Hardened.Requests.Caching.Memory`,
`Hardened.Requests.Serializers.Newtonsoft` — ships a `[DependencyModule]` registering services. It
is now `[DashboardModule]` over eight services with `IWidgetConsole` composed on top, all `TryAdd`
so a stricter sanitizer or a measured stylesheet can replace one. Section 6's stated reason for the
library existing is that the harness and the test driver cannot be allowed to drift; a registration
enforces that and two static classes do not.

**Test the requirement, not the implementation.** Having written it static, the tests needed no
container, and that was used as an argument that none was needed — reasoning from the mistake
rather than to it. The suite is now `WhatTheConsoleSendsTests` and `WhatTheConsoleShowsTests`
through `IWidgetConsole` resolved by `[HardenedTest]`, with names like
`ARefreshReturnsTheWidgetToItsLandingPage` and `AnUncheckedBoxSendsNothing`. What stayed direct is
where the implementation *is* the requirement: the sanitizer's list, the ARN-to-function-name split,
the faults an author is told about. Hardened's own suite splits the same way.

`[HardenedTest]` resolves from `[assembly: DashboardModule]` with no application entry point, which
was worth checking rather than assuming.

**Mutation-checked, and one test was a lie.** Sixteen tests passed on the first run, which is a
reason for suspicion rather than confidence, so two deliberate breaks went in.
`AnActionInsideAStrippedElementCannotBeClicked` survived a mutation that read actions from the raw
response instead of the sanitized one — it had been written with `<iframe>`, whose content every
HTML parser treats as text, so there was never an action inside it to find. `<use>` is the one
removal of the three whose children are parsed as elements. The test now bites, and the reason is
written above it.

Also worth recording: a `dotnet build` segfaulted once during restore, exit 139, and the identical
command succeeded immediately after. Not reproducible, not filed.
## 2026-09-07 — The widget adapter, and two things the framework said no to

Item 2, first half. `LambdaWidgets.Runtime` is the adapter, the web-shaped request, the typed
context and `[LambdaWidgetModule]`. The Razor helpers, describe and the Echo sample are the second
half.

The module composes `[HardenedWebModule]` and `[LambdaRuntimeModule]`, so a widget application still
writes two attributes. Without the web module there is no route table and the invocation fails with
"This function declares no handlers", which names neither the missing module nor the widget one.
Day-one check 7 found that; this is where it is fixed rather than in every sample.

**Plan section 5.2 did not work as written, and a passing test hid it.** It says the merge becomes
the query string and a handler binds one request object from it "with the ordinary binding". Hardened
binds a complex parameter from the *body* — documented, deliberate, and the same on Kestrel. The
query string was correct and `SearchRequest` came back empty.

What cost the most time was that one of the tests passed for the wrong reason. Sending `limit` as a
top-level event field meant `SearchRequest` deserialized straight out of the widget event, which
looked exactly like successful query binding. Two of the three merge tests were vacuous. That is the
second time in two days a test has passed for a reason unrelated to its name, and both times the
tell was the same: a suspicious first-run pass.

So the merge is now the query string *and* the body. The query string is strings, for
`[FromQueryString]`; the body is JSON with each value's original type preserved, for a complex
parameter. Types survive because the framework's deserializer defaults to `AllowReadingFromString`,
so a form field's `"20"` reaches an `int`.

**Then the framework refused the whole approach, and was right to.** `HRDR010`: a complex parameter
on a `[Get]` is read from the body, and a GET carries none. True of HTTP, false of a widget, whose
GET is a scheme label. `[FromWidget]` answers it properly — `ICustomBindingAttribute` is a source of
its own, so the rule does not apply, and the attribute says at the handler what is happening. It
binds through `ISerializationLocatorService` rather than reflecting over the type, which keeps it
AOT-safe. Finding F-10.

Worth recording that the custom attribute was the maintainer's call before any of this surfaced, on
a worse argument than the one that turned out to justify it. The reason offered at the time was type
conversion, which `AllowReadingFromString` had already solved.

`WidgetRequest` enrols in the payload conformance profile, not the web one. The web profile's three
extras are a query string, decoded values and cookies; a widget has the first two and can never have
the third. Enrolling in the web profile would mean giving the request a cookie list it never
carries, so the two that apply are written out in the test class instead. Finding F-09.

39 tests: 12 requirements through the real `LambdaInvocationHandler`, 25 conformance, 2 written out.

## 2026-09-08 — The Razor helpers, and a bug only the console would have found

Item 2, second half, less describe and Echo. `Widget.Button`, `Link`, `Confirm`, `Detail`,
`Action`, `Popup`, `Hover` and `Root` write the console's `cwdb-action` from a route the generated
`Links` produced.

**Plan section 5.4's `@inherits LambdaWidgets.Runtime.WidgetTemplate<ResultsPage>` was wrong.**
Hardened generates a per-application view base from `[Enable<RazorTemplates>]`, and that generated
base is what carries `Links`. A view inheriting the runtime's own base directly would get the
helpers and no links, so every route in it would be a literal — exactly the failure the helpers
exist to prevent. So `LambdaWidgets.Runtime` ships a `[TemplateBase]` marker of its own,
`WidgetTemplates`, and a view inherits `LogsSearchWidgetTemplates<ResultsPage>`. A handler names its
view with `[Output<Views.ResultsPage>]`, which the plan does not mention either.

**A rendered view answered with something the console cannot read.** The Invoke API returns JSON and
the console renders the string it finds. A handler returning a `string` or an object is serialized
by the IO filter and is JSON already; a view is not — the template writes raw markup into the
response body, and the adapter copied it straight out. Every template-based widget would have failed
in the console and passed every test that read the body directly. The adapter now quotes a
`text/html` response on the way out, which is the only place that can happen.

That one is worth sitting with. It is not the kind of defect a unit test finds, because nothing
local is wrong: the template renders correctly, the adapter copies correctly, and the two together
produce an answer no caller can parse. What found it was rendering a real view through the real
loop and looking at the bytes.

The tests read the rendered widget back through `LambdaWidgets.Dashboard` rather than matching
strings. That is the first place the two halves of the repository meet, and it is a better assertion
than any string comparison: what the helpers emit has to be something the interpreter can find an
action in, because the interpreter is where the console's behaviour is modelled.

Two smaller things. `PrivateAssets="all"` on the RazorBlade reference makes its types internal to
the consuming assembly, so a public helper returning `IEncodedContent` is `CS0050`; the framework's
own `Hardened.Templates.RazorBlade` references it without `PrivateAssets` and says why in a comment.
And the helper class was called `WidgetActions`, which collides with the interpreter's reader of the
same name — it is `WidgetHelpers` now, and the interpreter keeps the better name.

Mutation-checked: never quoting a view fails nine tests, hard-coding the endpoint fails one.
49 tests.
