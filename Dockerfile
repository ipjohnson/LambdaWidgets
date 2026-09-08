# The harness as an image, for a widget author whose language is not C#.
#
# Two stages, and the second one carries no runtime at all: the binary is native, so what it needs
# is a C library and a certificate store rather than .NET. That is what keeps the image the size of
# the binary plus a base rather than the size of a framework.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Restore before the source is copied, so a change to a .cs file does not re-restore. The props
# files are what the restore reads.
COPY Directory.Build.props Directory.Packages.props nuget.config global.json LambdaWidgets.slnx ./
COPY src/ src/
COPY tests/ tests/
COPY samples/ samples/

# clang and zlib are ILC's linker and its compression, and neither is in the SDK image.
RUN apt-get update \
 && apt-get install --yes --no-install-recommends clang zlib1g-dev \
 && rm -rf /var/lib/apt/lists/*

RUN dotnet publish src/LambdaWidgets.Harness \
      --configuration Release \
      -p:PublishAot=true \
      --output /app

FROM debian:bookworm-slim

# ca-certificates because --aws talks to the Lambda service over TLS, and libgcc/libstdc++ because
# a native binary still links the C++ runtime ILC emitted against.
RUN apt-get update \
 && apt-get install --yes --no-install-recommends ca-certificates libgcc-s1 libstdc++6 \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/lambda-widgets /usr/local/bin/lambda-widgets

# The same port the harness listens on everywhere else, and the one the Kestrel host defaults to.
EXPOSE 5080

# A dashboard file is mounted rather than baked in: it is the artifact the author is developing,
# and an image that carried one would be an image with somebody else's dashboard in it.
#
#   docker run --rm -p 5080:5080 -v "$PWD/dashboard.json:/dashboard.json" \
#     lambda-widgets --dashboard /dashboard.json --test-tool
#
# The tool it invokes runs on the host, so on Docker Desktop that is
# --function <name>=http://host.docker.internal:5050.
ENTRYPOINT ["lambda-widgets"]
CMD ["--dashboard", "/dashboard.json"]
