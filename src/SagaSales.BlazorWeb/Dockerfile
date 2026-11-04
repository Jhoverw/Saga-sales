# syntax=docker/dockerfile:1
# Build context is the repository root (see deploy/docker-compose.yml).
# A Blazor WebAssembly app is static files after publish, so it is served by nginx.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props global.json ./
COPY src/SagaSales.Shared/SagaSales.Shared.csproj src/SagaSales.Shared/
COPY src/SagaSales.BlazorWeb/SagaSales.BlazorWeb.csproj src/SagaSales.BlazorWeb/
RUN dotnet restore src/SagaSales.BlazorWeb/SagaSales.BlazorWeb.csproj

COPY src/ src/
RUN dotnet publish src/SagaSales.BlazorWeb/SagaSales.BlazorWeb.csproj \
    -c Release -o /app/publish --no-restore

FROM nginx:alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/SagaSales.BlazorWeb/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
