FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish applications/OpenGitBase.ComputeAgent/OpenGitBase.ComputeAgent.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends tini \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
# tini is PID 1, not dotnet: the kernel drops the SIGABRT an init raises on itself, so an
# unhandled exception would hang the process instead of exiting for the restart policy.
# tini also reaps job processes that end up orphaned to PID 1.
ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "OpenGitBase.ComputeAgent.dll"]
