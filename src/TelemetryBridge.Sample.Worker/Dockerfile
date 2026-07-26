# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN --mount=type=cache,id=telemetrybridge-nuget,target=/root/.nuget/packages \
    dotnet restore src/TelemetryBridge.Sample.Worker/TelemetryBridge.Sample.Worker.csproj
RUN --mount=type=cache,id=telemetrybridge-nuget,target=/root/.nuget/packages \
    dotnet publish src/TelemetryBridge.Sample.Worker/TelemetryBridge.Sample.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "TelemetryBridge.Sample.Worker.dll"]
