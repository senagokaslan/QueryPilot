FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY .config/ .config/
COPY src/QueryPilot.Api/QueryPilot.Api.csproj src/QueryPilot.Api/
RUN dotnet restore src/QueryPilot.Api/QueryPilot.Api.csproj
RUN dotnet tool restore

COPY src/QueryPilot.Api/ src/QueryPilot.Api/
RUN dotnet publish src/QueryPilot.Api/QueryPilot.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false
RUN dotnet tool run dotnet-ef migrations bundle \
    --project src/QueryPilot.Api/QueryPilot.Api.csproj \
    --startup-project src/QueryPilot.Api/QueryPilot.Api.csproj \
    --configuration Release \
    --self-contained \
    --target-runtime linux-x64 \
    --output /app/querypilot-migrate

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
COPY --from=build /app/publish .
COPY --from=build /app/querypilot-migrate .

USER $APP_UID
ENTRYPOINT ["dotnet", "QueryPilot.Api.dll"]
