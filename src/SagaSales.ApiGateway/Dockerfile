# syntax=docker/dockerfile:1
# Build context is the repository root (see deploy/docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first: restore stays cached while only source changes.
COPY Directory.Build.props global.json ./
COPY src/SagaSales.Shared/SagaSales.Shared.csproj src/SagaSales.Shared/
COPY src/SagaSales.BuildingBlocks/SagaSales.BuildingBlocks.csproj src/SagaSales.BuildingBlocks/
COPY src/SagaSales.ApiGateway/SagaSales.ApiGateway.csproj src/SagaSales.ApiGateway/
RUN dotnet restore src/SagaSales.ApiGateway/SagaSales.ApiGateway.csproj

COPY src/ src/
RUN dotnet publish src/SagaSales.ApiGateway/SagaSales.ApiGateway.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "SagaSales.ApiGateway.dll"]
