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

## 2026-09-08 — Describe, Echo, and item 2 closed

Describe is a filter that answers before dispatch. The console sends the widget's configured
parameters on a describe invocation exactly as it does on any other, so there is a route in the
event and a handler that would match it — running it would do the widget's work and throw the answer
away. On a search widget a describe would run the search. Returning without calling `chain.Next()`
is the whole mechanism, and `LambdaInvocationHandler` appending dispatch to the end of the chain on
first invocation is what makes a startup-registered filter land ahead of it.

A widget registers `IWidgetDocs`. The default says nothing, because AWS recommends answering
describe even with an empty string and the console's button is there either way.

Echo is the sample AWS documents, so a reader can compare it line for line with the Python and
JavaScript versions. It renders the `echo` parameter unescaped and shows the `widgetContext` under
it — which is the sample's real job, because a widget author's first question is what the console
actually sends, and that is easier to read on a dashboard than in a log. It is also the probe
day-one check 3 needs: feeding markup through `echo` and reading back what survived is how the
console's sanitizer gets documented.

`Echo.Tests` is the sample's test and the framework's integration test at once. Every piece of the
runtime is in the path — adapter, merge, context, view base, helpers, describe — and none of it is
named in an assertion. What is asserted is what a viewer would see, read back through
`LambdaWidgets.Dashboard`.

One assertion of mine was wrong and the suite caught it:
`DescribeAnswersWithDocumentationRatherThanTheWidget` asserted the answer omits `Hello world`, which
fails because the yaml block the console lifts uses exactly that as its example. The right way to
assert it is `DescribeDoesNotRunTheWidget`, with an echoed value the documentation does not itself
contain.

Mutation-checked: never short-circuiting describe fails three tests. 93 tests across the solution.
## 2026-09-08 — The harness, and the first thing anyone can look at

Item 3. `lambda-widgets` is a Hardened Kestrel application that loads a dashboard file, invokes each
widget through the Invoke API, and renders what comes back through the same `IWidgetConsole` the
test driver will resolve.

The shape that matters: **the page holds no rules.** Its JavaScript reports which bound element was
clicked and what is in the form fields, and nothing else. Which action that was, what the event
carries, what the console would have stripped — all server side, in the interpreter. A page that
decided any of it would be a second implementation of the console and the two would drift, which is
the failure section 6 exists to prevent.

The inspector is the part the console does not have and the reason to develop here at all. A widget
author whose button does nothing gets, on a real dashboard, a button that does nothing. Here they
get the event that was sent, what the sanitizer removed, the actions it found with their faults, and
how long the invoke took.

Ran it rather than assuming it, which caught the one wiring mistake: `[HardenedWebModule]` brings
the routing table and the request pipeline but not a host, so the application started and answered
every request with "No service for type IHttpApplication". `[KestrelRuntime]` is the module that
registers the host, and it composes the web module itself. The first request after that rendered a
widget, stripped a `<script>`, reported the removal and showed the event — which is the whole of
item 3 in one response.

Mutation testing paid again, and differently this time. Two of three mutations failed a test as
expected; the third, ignoring `X-Amz-Function-Error` entirely, passed everything. The test that was
supposed to cover it used the invoker double, which sets the error itself and never reads a header.
Two tests now drive `WidgetInvoker` against real HTTP responses. That is the second time a double
has made a test look like it covered something it never touched.

14 tests here, 107 across the solution.

The CI-flags build then failed with fifteen `xUnit1051`, which Hardened's own AGENTS.md warns about
by name: an optional `CancellationToken` on a shared test helper is a warning locally and an error
under `ContinuousIntegrationBuild`, so it lands at every call site at once and only in CI. Passing
`TestContext.Current.CancellationToken` rather than `default` is the fix. Reading someone else's
AGENTS.md before writing the tests would have been cheaper than reading it afterwards.

## 2026-09-08 — The click-through driver, and thirty lines that went away

Item 4. `IWidgetDriver` opens a widget, fills its fields, clicks something by the text a viewer
would read, and hands back what the console would show.

The rule it is built around: **nothing a test writes names a route, a payload or a JSON field**,
because a viewer cannot name one either. `widget.Click("Run query")` breaks when the button's text
changes, which is a behaviour change; it does not break when the route behind it is renamed, which
is not. A test written against the payload has that backwards, and every widget test in this
repository before today was written that way.

In process, through the real `LambdaInvocationHandler`, and read back through `IWidgetConsole`.
So the adapter, the merge, dispatch and the response path are all exercised, and what the driver
says is on screen is what the harness would render — which is the property that makes a passing
driver test worth anything.

The proof is `Echo.Tests`. It had a hand-written `ILambdaContext`, JSON payloads as string literals,
and a helper that unwrapped the Invoke response in every test. All of it is gone; the tests say what
they always meant and are shorter for it. That deletion is item 4's argument.

`Decline` is the piece worth having deliberately. A confirmation exists to make a destructive action
not happen, and nobody checks that until it has failed once. `DecliningAConfirmationChangesNothing`
asserts the screen is the same object it was.

Mutation-checked three ways: a refresh that keeps the viewer's edits, a decline that invokes anyway,
and a fill that accepts a field the widget does not have each fail exactly one test.

121 tests across the solution.

## 2026-09-08 — The search sample, and the AOT claim checked for real

Item 5. A Logs Insights search as a widget: a form the viewer types into, the dashboard's range
reaching a real query, an AWS SDK client under ahead-of-time compilation, and a call slow enough
that the console waits on it.

**The time range comes from the context, not from a field**, and that is the design decision the
sample is really demonstrating. A widget beside a graph should search the window the viewer is
looking at and follow them when they zoom it. Putting a time field on the form would be a second
place for it to be wrong, and two tests hold it: one for the dashboard's range, one for a zoom
narrowing the search.

The polling sits behind `ILogQueries` rather than in the handler, because it is the SDK's shape and
not the widget's — Logs Insights has no run-and-wait call. That is also the sample's demonstration
of the shape a widget lives with: there is no second round trip to come back in, so the work happens
inside the invocation or not at all, which is what the harness's 60-second proxy timeout was written
for. A query abandoned when the invocation runs out of time is stopped, because one left running
keeps scanning and keeps being billed for a widget nobody is looking at.

The tests substitute at `ILogQueries` rather than at `IAmazonCloudWatchLogs`, deliberately. Mocking
the SDK client would assert that the widget calls `StartQuery` and polls `GetQueryResults` the way
the test imagines, which is implementation and free to change. Substituting the query records what a
viewer's search actually asked for.

**Day-one check 1's recheck is answered, and it holds.** The original probe covered construction and
request types rather than a working widget, and said so. `samples/LogsSearch` publishes `PublishAot`
with zero `IL2xxx` or `IL3xxx` — 17 MB carrying the Hardened runtime, the widget adapter, RazorBlade
views and `AWSSDK.CloudWatchLogs` with its responses deserialized. Nothing in the stack needs a trim
hint, which is the claim the AOT pin rests on and the first time it has been true of real code
rather than a throwaway.

134 tests.

## 2026-09-08 — The lookup sample, and paging with nowhere to put state

Item 6. A DynamoDB lookup by key, with a Next link.

The sample exists for one line:

    @Widget.Link("Next", "/look-up", ("cursor", Model.Page.Cursor))

A widget gets no cookies, no local storage and no state between invocations, so where the viewer is
up to is a property of the link they are about to click and of nothing else. Everything else in the
sample is there to make that line meaningful — the cursor arrives in the request like any other
value, leaves in the next action's fields, and is opaque to the widget in between.

`ARefreshLosesThePageTheViewerWasOn` asserts the consequence rather than working around it. The
console re-invokes with the configured parameters, and the cursor was never among them, so a refresh
puts a paging widget back on page one. That is what the console does, and a paging widget has to be
designed for it.

The table is the dashboard author's and not the viewer's, deliberately. A widget whose table a
viewer could change by clicking is a widget whose execution role has to allow every table, which is
the opposite of what scoping a role is for. There is a test for that too, because it is the kind of
thing a later refactor quietly loosens.

**F-06's claim is now tested from outside.** #307 is merged and unreleased, so the sample takes
`Hardened.Amz.DynamoDbClient` at 0.22 alongside the 0.30 pins — which is exactly what F-06 said
would work. A throwaway consumer resolved `IDynamoDbClientProvider` from a container built by
`[DynamoDbModule]` and got a working client, and the sample then published AOT with zero IL warnings
carrying the frozen package and the 0.30 line in one binary. The one exception to "one version line"
now has a verification behind it rather than an argument, and `Directory.Packages.props` says when
to delete it.

The first probe failed on a missing `IConfigurationManager`, which was my container and not the
package. Worth writing down only because the failure names a Hardened type and reads like the
package being broken.

146 tests.

## 2026-09-08 — The stack, and what a template test is for

Item 7, the half that does not need an account. `samples/deploy` is a plain `Amazon.CDK.Lib` app:
two widget functions on `provided.al2023`, a role each, the dashboard, and one managed policy a
viewer's role attaches.

Plain CDK because there is no Hardened construct on this line — `Hardened.Amz.Cdk` stopped at 0.22
and depends on the Lambda host 0.30 replaced, so it cannot be taken beside it. Finding F-05. Writing
it by hand also documents what the eventual construct has to cover, which is the more useful thing
to hand upstream than a complaint.

**The dashboard file is one artifact.** `dashboard.json` is what the harness renders locally and
what gets deployed, with the placeholder endpoints substituted for the real ARNs. Authoring the
deployed one separately would be two dashboards that drift, and the drift would be invisible until
someone compared them.

The tests assert on the synthesized template, which is the part of infrastructure worth testing: not
that CDK works, but that the role is scoped, that the dashboard names the functions actually
created, and that the runtime matches what the binary is. All three are things a reviewer cannot see
at a glance and an account finds out expensively.

`TheSearchWidgetsRoleAllowsOnlyTheQueriesItMakes` is the one that matters. The role is the whole of
what the tool may do, and it is the argument the rationale document makes for widgets over scripts —
a script runs with whatever its author's credentials allow. A test that only checked the widget
*could* query would pass just as happily on `logs:*`.

`Template.ToJSON()` answers a dictionary, and its `ToString()` is the type name — which parses as
JSON exactly as well as it sounds, and cost three failing tests to notice.

Mutation-checked: widening the role to `logs:*`, leaving the placeholder endpoints in the dashboard
body, and swapping the native runtime for the managed one each fail exactly one test.

155 tests. Day-one check 3, the probe, is what is left, and it needs an account.

## 2026-09-08 — Distribution, and the binary that had to start

Item 8. A release workflow on a `v*` tag, a Dockerfile, and the README pitch. The dotnet tool was
already half done — the harness carried `PackAsTool` from the scaffold.

**Checked before claiming, in the order that mattered.** The plan says the harness ships as a native
binary, so before writing a workflow that assumes it: `dotnet publish -p:PublishAot=true` on the
harness emits zero `IL2xxx` or `IL3xxx` and produces 16 MB carrying AngleSharp, Markdig, RazorBlade
views, Kestrel and the static content. Then the binary itself was started and asked for a widget,
and it rendered one, stripped a `<script>` and reported the removal. Day-one check 5 said the parsers
publish clean; this is the first time the whole application has.

The workflow has an **It runs** step for the same reason. A publish that succeeded and a binary that
cannot reach its own embedded static content are the same green check, and only starting it tells
them apart. It is five lines and it is the step most likely to earn its place.

One matrix job per runner architecture, because ahead-of-time compilation does not cross-compile:
ILC emits native code for the machine it runs on, so a `linux-arm64` binary needs a `linux-arm64`
runner. That is the reason distribution is a matrix rather than one publish with five `-r` flags,
and it is worth a comment because the shape looks like over-engineering until you know.

Two things borrowed from Hardened's own release, both of which its AGENTS.md records as having gone
wrong: the pack list is named rather than globbed, and the expected count is a literal. A packable
project silently dropped from a glob ships a release missing that package and says nothing, which is
a failure a consumer discovers rather than CI. The push to nuget.org is last, because a package
cannot be unpublished and it is the one step with no way back.

All four packages pack, verified rather than assumed, and the harness's tool manifest declares the
`lambda-widgets` command.

## 2026-09-08 — The linter and the API, and the last item that could be built

Item 9. The linter reports the mistakes the console makes no noise about, and the HTTP API lets a
widget author who is not writing C# drive a click-through.

Every linter rule is a widget that renders and then does nothing, which is the worst failure a
widget has because it looks finished. An `onclick` is the one an author hits first, since it is how
every other page on the web responds to a click; the console strips it silently. An action with
another element between it and its button binds nothing. A field with no name renders, gets typed
into, and is the one thing the click does not carry.

Findings travel with the shown widget rather than being fetched separately, so the inspector, the
driver and the API all have them without asking. They are read from the raw answer rather than the
sanitized one, because half of them are about what the sanitizing removed and there is nothing left
of those afterwards.

`AWellFormedWidgetHasNothingWrongWithIt` is the rule that keeps the rest honest. It lints the shape
the Razor helpers emit, and a linter that reported anything there would be one nobody left switched
on.

The API's argument is narrow and worth stating: anyone can post JSON to a Lambda emulator. What
nobody outside this repository can do is work out what the console would send for a click, or what
it would have stripped before rendering. That is what these endpoints are for, and it is why they go
through the same interpreter the page and the driver use rather than a second model of the console.

Driven end to end with curl before claiming it: post a dashboard, open a widget, read back its
forms, its actions and its findings, then click by index with what the viewer typed. The one thing
the session showed that a test did not was the stand-in tool reading `query` from the top level and
finding nothing — form values arrive under `widgetContext.forms.all`, and a fake that reads the
wrong place is a fake being simplistic rather than a defect.

172 tests. Every item in the plan that can be built without an AWS account is now built.

## 2026-09-08  Charts, and pointing at them

`LambdaWidgets.Charts`. A widget that queries something usually wants to draw the answer, and the
console strips `<script>`, so the chart has to arrive already drawn. SVG, server-side, from a
`Chart` a handler describes rather than draws.

The library makes three decisions a widget author would otherwise make wrong. A one-row breakdown is
a number, not a one-bar bar chart. Past three series the tail is summed into `Other` rather than
dropped. There is no dual axis, at all, because a second y-scale invents a correlation that is not
in the data.

The palette was validated rather than picked: four categorical slots per theme, checked against the
console's own surfaces for the lightness band, the chroma floor, colour-vision separation and the
normal-vision floor. The fourth slot exists because folding produces a fourth line. `For` used to
wrap modulo three, which handed `Other` the same blue as the largest series — a repeated colour that
looks like data. It throws now, and `ChartLimits.Fold` is what keeps the count in range.

The theme is resolved on the server. A web page guesses the reader's theme from
`prefers-color-scheme`; a widget does not have to, because the console sends its own theme in the
event.

### The hover layer

Asked for a crosshair that shows every series at one moment. CSS `:hover` and `:focus-within` over
one transparent band per data position, which is not JavaScript and survives the sanitizer.

Two things came out of building it that reading would not have.

The first: a card that follows the pointer covers the data. The first version floated a readout
beside the crosshair, and on screen it sat over the neighbouring bins — hiding exactly the
comparison the reader was hovering to make. The card is a hundred pixels wide and a bin is sixteen,
so no amount of per-bin repositioning fixes it. The readout moved to a reserved row under the axis:
it hides nothing, it lands in the same place every time, and it costs twenty pixels of height.

The second: hidden by attribute, revealed by stylesheet, never the other way round. Each readout
carries `opacity="0"` and the rule only turns it on. Whether the console keeps a `<style>` block is
on the probe's list; if it does not, the chart draws plain. A rule that did the hiding would have
put thirty-six readouts on screen at once.

Columns got the same treatment, because two charts in one widget where only one responds reads as
broken. The band is the hit target rather than the column — a 24px column in a 114px band is a
pinpoint — and the readout spells out the name the axis had to truncate, which is a legibility bug
the hover layer happened to fix.

None of this gates anything. Every chart ships a `<details>` table with every value in it.

### What the screenshots caught

Four defects, none of which a test would have found, all of which one look did: the floating card
occluding lines, two adjacent column labels running together, the axis truncating a name with
nowhere else to read it, and the readout itself truncating that name to fifteen characters after
being given the job of showing it in full.

Verified in the browser with a real hover and a real focus rather than assumed: click a band, move
the pointer to the far side of the page, and the readout stays — which is the keyboard path.

189 tests. The seventeen new ones were mutation-tested five ways — readouts visible at rest, the
focus rule removed, the series names dropped, the palette cycling again, a single category drawing
a column — and each mutation failed a test.

### Two harness gaps

The API can set the theme and the page cannot. `PUT /api/dashboards/{id}/state` with
`{"theme":"dark"}` works, and the widget comes back in the dark palette; there is no control on the
harness page to do it, so the theme where charting palettes actually break is the one nobody can
see locally. Worth a control.

An unknown widget id answers `500` with an empty `details`. The message the harness raises is good —
it names the ids the dashboard does have — and the HTTP layer discards it. That is every route's
error mapping rather than this one, so it is noted rather than patched here.
