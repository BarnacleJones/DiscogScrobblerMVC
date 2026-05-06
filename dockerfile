FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# The .csproj runs `npm run build` via MSBuild — the SDK image has no Node.
RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY . .
RUN cd DiscogScrobblerMVC && npm ci
RUN dotnet publish DiscogScrobblerMVC/DiscogScrobblerMVC.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir -p /app/data /app/logs /app/images

ENTRYPOINT ["dotnet", "DiscogScrobblerMVC.dll"]