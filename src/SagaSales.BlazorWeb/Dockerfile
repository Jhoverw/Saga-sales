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
# Set by scripts/build-label.ps1 (version, git commit, dirty flag, UTC time). The panel shows it.
ARG SAGA_BUILD_LABEL=unlabeled
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
# SPA fallback: deep links (/caja, /orders...) return index.html. Without this file nginx's default config
# answers 404 for any URL typed or reloaded directly.
COPY src/SagaSales.BlazorWeb/nginx.conf /etc/nginx/conf.d/default.conf
RUN mkdir -p /usr/share/nginx/html/assets && printf '%s' "$SAGA_BUILD_LABEL" > /usr/share/nginx/html/assets/build-id.txt

EXPOSE 80
