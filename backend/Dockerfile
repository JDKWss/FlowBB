# Wersje przypiete (SDK 10.0.401, runtime 10.0.12), zeby demo budowalo sie tak samo w dniu prezentacji.
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src

COPY backend/src/FlowBB.Domain/FlowBB.Domain.csproj backend/src/FlowBB.Domain/
COPY backend/src/FlowBB.Application/FlowBB.Application.csproj backend/src/FlowBB.Application/
COPY backend/src/FlowBB.Infrastructure/FlowBB.Infrastructure.csproj backend/src/FlowBB.Infrastructure/
COPY backend/src/FlowBB.Api/FlowBB.Api.csproj backend/src/FlowBB.Api/
RUN dotnet restore backend/src/FlowBB.Api/FlowBB.Api.csproj

COPY backend/src/ backend/src/
# FlowBB.Infrastructure osadza database/flowbb-demo-seed.cypher jako zasob (EmbeddedResource), wiec katalog musi byc w obrazie budujacym.
COPY database/ database/
# Deterministyczny fallback Air Quality jest osadzany z wersjonowanego snapshotu.
COPY data/air-quality/ data/air-quality/
# Planer transportu publicznego osadza wersjonowany rozklad MZK jako zasoby assembly.
COPY data/gtfs/mzk/parsed/departures.json data/gtfs/mzk/parsed/departures.json
COPY data/gtfs/mzk/parsed/calendar_days.json data/gtfs/mzk/parsed/calendar_days.json
COPY data/gtfs/mzk/parsed/stops.json data/gtfs/mzk/parsed/stops.json
RUN dotnet publish backend/src/FlowBB.Api/FlowBB.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "FlowBB.Api.dll"]
