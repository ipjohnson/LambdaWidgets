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
| `src/LambdaWidgets.SourceGenerator` | The `.App` file: `Main` and `Invoke` for the widget event |
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

## Hardened and Amz move together

Both are pinned at `0.21.0-rc1000` in `Directory.Packages.props`, the last line both released.

Hardened.Framework is on `0.22.0-rc1000` and Hardened.Amz is not. Taking the newer framework alone
would pair Amz assemblies with a framework they were never compiled against. Bump both together when
Amz `0.22.0-rc1000` ships, and delete finding F-02 when it does.

## The generator compiles three packages from source

`LambdaWidgets.SourceGenerator` needs four opt-in properties and three `PrivateAssets=all` package
references, and every one of them is load-bearing. They are commented in the csproj.

An analyzer is loaded by the compiler with no probing path of its own, so a sibling DLL is a
`FileNotFoundException` at initialization. That is why `Hardened.SourceGenerator`, `CSharpAuthor`
and `ValidationModules.SourceGenerator.Impl` ship `.cs` under `src/` in the package instead of an
assembly, and why each needs a property to switch its own inclusion on: `PrivateAssets=all` means no
package can flow the switch to the project that consumes it.

**CSharpAuthor must be exactly `2.0.0-preview1005`**, which is what Hardened.Framework builds its
generators against. Not "at least". From 2.0 the `Global` output mode qualifies types in the global
namespace, so the version decides what the compiled-in generators *emit*, not only whether they
compile. The package's own `HARDENED001` guard will not catch a mistake here; see finding F-01.

`RS2008` is suppressed for `*SourceGenerator*` projects because those 150-odd compiled-in files
bring their own `DiagnosticDescriptor`s and nothing here can add release tracking for another
repository's rules.

## Two SDKs

`global.json` pins the .NET 10 SDK. Every project targets `net8.0`, and a test assembly is
framework-dependent on `Microsoft.NETCore.App` 8.0.0 with no `rollForward`, so a machine with only
the .NET 10 SDK compiles everything and then starts no test host. Install both.

Hardened.Framework pins a .NET 11 preview because its own source uses a C# 15 `union`. Nothing that
reaches this repository does, so the stable SDK is enough here. If that changes it will arrive as a
compile error in a file under `~/.nuget/packages`.

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
