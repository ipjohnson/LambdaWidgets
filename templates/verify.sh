#!/usr/bin/env bash
# Generates a project from every template and builds and tests it, against packages packed from
# this working tree.
#
# The templates reference LambdaWidgets by version, the way a real consumer will. Nothing is on
# nuget.org yet, so this packs the current source at a local version and points the generated
# projects at a folder feed. When the first tag ships, only the default version changes.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="${1:-$(mktemp -d)}"
# Unique per run, and that is load-bearing. NuGet resolves a package from the global cache by id
# and version, so repacking the same version leaves an earlier extraction in place and the
# generated projects build against whatever src/ looked like the first time. This check passed for
# hours against a stale package after the source it was meant to be verifying had changed.
VERSION="1.0.0-local$(date +%Y%m%d%H%M%S)"
FEED="$WORK/feed"

echo "==> packing $VERSION into $FEED"
rm -rf "$FEED" && mkdir -p "$FEED"

for project in Dashboard Runtime Testing Charts; do
    dotnet pack "$ROOT/src/LambdaWidgets.$project/LambdaWidgets.$project.csproj" \
        --configuration Release -p:Version="$VERSION" --output "$FEED" --nologo --verbosity quiet
done

# The pack too, and the templates are installed from it rather than from the folder. The layout
# inside the nupkg is its own thing to get wrong, and installing the folder would never catch it.
dotnet pack "$ROOT/templates/LambdaWidgets.Templates.csproj" \
    --configuration Release -p:Version="$VERSION" --output "$FEED" --nologo --verbosity quiet

echo "==> installing the template pack"
dotnet new uninstall LambdaWidgets.Templates > /dev/null 2>&1 || true
dotnet new install "$FEED/LambdaWidgets.Templates.$VERSION.nupkg" --force > /dev/null

# A folder feed and nothing else for LambdaWidgets, so a stale package from the global cache cannot
# be what the generated project actually built against.
cat > "$WORK/nuget.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML

for template in lw-logs lw-ddb lw-graph; do
    out="$WORK/$template"

    echo "==> $template"
    rm -rf "$out" && mkdir -p "$out"
    cp "$WORK/nuget.config" "$out/nuget.config"

    dotnet new "$template" --name Probe --output "$out" --widgetsVersion "$VERSION" > /dev/null

    # Named explicitly rather than globbed: a project the template stopped emitting would
    # otherwise pass by not being there.
    # warnaserror because the widget declares IsAotCompatible: an IL2xxx or IL3xxx here is a
    # widget that publishes fine and throws once it is deployed.
    dotnet build "$out/Probe/Probe.csproj" --configuration Release --nologo --verbosity quiet -warnaserror
    dotnet test "$out/Probe.Tests/Probe.Tests.csproj" --configuration Release --nologo --verbosity quiet

    # The assembly name is the Lambda handler, and it is what a deployment registers.
    test -f "$out/Probe/bin/Release/net8.0/customWidgetProbe.dll" \
        || { echo "::error::$template did not produce customWidgetProbe.dll"; exit 1; }

    # No literal routes. The generated Links are the whole reason a renamed handler breaks the
    # build, and a template that taught otherwise would undo it. A route is a path, so a quoted
    # string starting with a slash is the tell; a button's text and a confirmation never are.
    if grep -rnE '(Widget\.(Link|Button|Confirm|Detail|Action)|DrillingTo)\([^)]*"/' \
            "$out/Probe/Views" "$out/Probe"/*.cs; then
        echo "::error::$template has a literal route where the generated Links should be"
        exit 1
    fi
done

echo "==> all three templates build, test and deploy-name correctly"
