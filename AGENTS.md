# LambdaWidgets

Invariants and traps for anyone editing this repository. `README.md` covers what the products are;
this file does not repeat it.

The plan of record is the implementation plan, and the order of work is its section 14. Execute it
rather than re-litigating it.

## Layout

The solution is `LambdaWidgets.slnx` at the root. It is the `.slnx` format rather than `.sln`
deliberately: it is plain XML, so a project silently dropped from it shows up in a diff.

| Path | Contents |
|---|---|
| `src/LambdaWidgets.Dashboard` | The console's rules as a library: events, actions, forms, sanitizer, styles. No host, no HTTP, no UI |
| `src/LambdaWidgets.Runtime` | The widget host, the Razor helpers, describe, the typed widget context |
| `src/LambdaWidgets.Testing` | The click-through driver |
| `src/LambdaWidgets.Harness` | The `lambda-widgets` application |
| `samples/` | Echo, Logs Insights search, DynamoDB lookup, and the CDK app that deploys one |
| `tests/` | One test project per `src` and `samples` project |
| `docs/` | The VitePress site published to GitHub Pages |

## Commands

```bash
dotnet tool restore
dotnet build LambdaWidgets.slnx
dotnet test  LambdaWidgets.slnx
```

Before opening a pull request, build the way CI does:

```bash
dotnet build LambdaWidgets.slnx -c Release -p:ContinuousIntegrationBuild=true
```

`ContinuousIntegrationBuild` sets `TreatWarningsAsErrors` (`Directory.Build.props`). Local builds
deliberately do not, so a build that is green locally can still fail CI on a warning.

**Check the exit code, not the tail of the output.** The first CI-flags build here produced 68
`RS2008` errors and 12 `CS0246` errors, and only the `RS2008` block fit in a `head -40` window. The
`CS0246` ones were the real defect. Capture to a file and test `$?`.

## Every Hardened reference is a package

No `ProjectReference` to a sibling Hardened or Amz checkout, ever, and `nuget.config` clears all
sources but nuget.org so there is nothing to fall back to. The local swap hides exactly the
packaging defects this repository exists to find. If a package is missing or wrong, the restore must
fail and the gap goes in `FINDINGS.md`.

## One version line

Everything Hardened is pinned at `0.30.0-rc1000` in `Directory.Packages.props`.

`Hardened.Amz` no longer exists. Framework PR #305 consolidated four repositories into
Hardened.Framework and rebuilt the AWS packages on a new host seam, so the two-line pin-back rule
this file used to carry is gone with it. The Amz packages stay on nuget.org at `0.22.0-rc1000`,
frozen. Nothing here takes one, and a version-skew problem between two Hardened lines cannot happen
any more.

Two Amz packages had no successor at 0.30 and matter to the plan. `Hardened.Aws.DynamoDbClient` and
`Hardened.Aws.DynamoDbClient.Testing` were restored upstream by Framework #307 and arrive on the
next line; finding F-06. `Hardened.Amz.Cdk` was not and cannot be taken beside 0.30, because it
depends on the old Lambda host; `samples/deploy` uses `Amazon.CDK.Lib` directly. Finding F-05.

## A widget attaches through IPayloadAdapter

**This repository authors no source generator, and consumes several.** Those are different things,
and confusing them cost a build here once already.

Nothing here emits code. The entry point of a widget application is a hand-written `Program.cs` that
asks `LambdaEmulator.StartIfLocal` for a session and hands a service provider to
`HardenedLambdaBootstrap.Run`. The framework's own web-on-Lambda template says why, in its csproj:
"No Lambda source generator: the entry point is Program.cs rather than something emitted."

Every project that declares a module or a route references generators, as any Hardened consumer
does. They are `developmentDependency` packages, so none flows past the project that names it and
each project names its own:

| Project | Generators |
|---|---|
| `LambdaWidgets.Runtime` | `DependencyModules.SourceGenerator`, `Hardened.Library.SourceGenerator` |
| `LambdaWidgets.Harness`, and every sample | those two and `Hardened.Web.SourceGenerator` |

Both are needed wherever a `[DependencyModule]` is declared, and getting it wrong does not fail
where the reference is missing. `Hardened.Library.SourceGenerator` alone compiles the module and
never emits its attribute, so the error lands on whoever applies it, as
`CS0616 'LambdaWidgetModule' is not an attribute class`.

**An empty project proves nothing about its analyzers.** Every `src` project is a stub until its
item lands, and an analyzer with no source to read emits nothing, so a missing generator reference
is invisible to a green build. That is how the reference set above was wrong through a full CI run.
Verify a toolchain claim by compiling something that uses it, not by building the stub.

What this repository writes instead is one `IPayloadAdapter` in `LambdaWidgets.Runtime`, and the
`[DependencyModule]` that registers it. The adapter turns a widget invocation into a web-shaped
`IExecutionRequest` and copies the handler's answer back out; `IRequestExecutor`, the route table
and the binding are the framework's and are not reimplemented. `ApiGatewayAdapter` is the model for
the request half and `InvokeAdapter` for the response half.

`src/LambdaWidgets.SourceGenerator` existed until the 0.30 uptake and compiled
`Hardened.SourceGenerator` in from source. Deleting it took `CSharpAuthor`,
`ValidationModules.SourceGenerator.Impl`, the four `PackageHardened*IncludeSource` properties and
the `RS2008` suppression with it. Findings F-01, F-03 and F-04 came out of building it and stay in
the log: they are about those packages, and they are still true for anyone who writes a generator.

**Do not reference `Hardened.DependencyModules.SourceGenerator`.** It is not packable on the 0.30
line and reaches a consumer inside `Hardened.Library.SourceGenerator`, which packs its DLL into its
own `analyzers/dotnet/cs`. The `0.1.0-rc1` still on nuget.org is four release lines behind and is
not the same generator.

## Two SDKs

`global.json` pins the .NET 10 SDK. Every project targets `net8.0`, and a test assembly is
framework-dependent on `Microsoft.NETCore.App` 8.0.0 with no `rollForward`, so a machine with only
the .NET 10 SDK compiles everything and then starts no test host. Install both.

Hardened.Framework pins a .NET 11 preview because its own source uses a C# 15 `union`. Nothing that
reaches this repository does, so the stable SDK is enough here. If that changes it will arrive as a
compile error in a file under `~/.nuget/packages`.

## Two logs

`FINDINGS.md` is the deliverable: one entry per thing that was awkward, missing or broken in
Hardened, Amz, RazorBlade, DependencyModules or ValidationModules. `DEVLOG.md` is the account of the
work — decisions and why, dead ends, corrections, what each probe cost. A mistake of our own goes in
`DEVLOG.md`; a defect in someone else's package goes in `FINDINGS.md`. Both are written when it
happens and not reconstructed later.

## The findings log

`FINDINGS.md` is the deliverable. One entry per thing that was awkward, missing or broken in
Hardened, Amz, RazorBlade, DependencyModules or ValidationModules, **written when it happens** and
not reconstructed later.

A finding fixed upstream gets its PR link and the version that shipped it. A finding the maintainer
dismisses is deleted from the log, not marked dropped.

## Documentation

The site is VitePress under `docs/`, published to GitHub Pages by `.github/workflows/docs.yaml` on
every push to `main`.

`base` in `docs/.vitepress/config.ts` is `/LambdaWidgets/` and has to stay that way while the site is
served from `ipjohnson.github.io`. Without it every asset URL on a built page resolves against the
org root and 404s, which a local `docs:dev` run will never show you.

**The reference section describes CloudWatch, not this repository.** It has to stay true whether or
not anything here ships, and a claim in it that has not been read from AWS's documentation or
observed on a real dashboard is marked **unverified**. `docs/status.md` is what says which items
exist; individual guide pages carry a warning block rather than hedging in prose.

## Publishing AOT on macOS

The harness ships as a native binary, so `dotnet publish -p:PublishAot=true` has to work locally.
On a machine with Homebrew LLVM on the path it fails at the *link* step, after ILC has already
generated the object file, with `ld: library not found for -ldl` and a missing-sysroot warning
naming a `CommandLineTools` SDK that is not installed:

```
clang: warning: no such sysroot directory: '/Library/Developer/CommandLineTools/SDKs/MacOSX26.sdk'
```

Homebrew's clang is being picked over Xcode's. Point the build at Xcode's toolchain:

```bash
export SDKROOT="$(xcrun --sdk macosx --show-sdk-path)"
export PATH="$(dirname $(xcrun -f clang)):$PATH"
```

The failure looks like an AOT problem in a dependency and is not one. Read the log above the link
step: if `Generating native code` finished with no `IL2xxx` or `IL3xxx`, the managed side is fine.

## One PR at a time

Work in a clone under `/private/tmp`, never under `/tmp`.
