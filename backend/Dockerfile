FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/FlowBB.Domain/FlowBB.Domain.csproj backend/src/FlowBB.Domain/
COPY backend/src/FlowBB.Application/FlowBB.Application.csproj backend/src/FlowBB.Application/
COPY backend/src/FlowBB.Infrastructure/FlowBB.Infrastructure.csproj backend/src/FlowBB.Infrastructure/
COPY backend/src/FlowBB.Api/FlowBB.Api.csproj backend/src/FlowBB.Api/
RUN dotnet restore backend/src/FlowBB.Api/FlowBB.Api.csproj

COPY backend/src/ backend/src/
# FlowBB.Infrastructure osadza database/flowbb-demo-seed.cypher jako zasob (EmbeddedResource), wiec katalog musi byc w obrazie budujacym.
COPY database/ database/
RUN dotnet publish backend/src/FlowBB.Api/FlowBB.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "FlowBB.Api.dll"]
