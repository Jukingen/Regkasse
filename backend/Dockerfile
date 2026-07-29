# syntax=docker/dockerfile:1

# -----------------------------------------------------------------------------
# Regkasse backend API — multi-stage production image (net10.0, self-contained)
#
# Build context MUST be the repository root (ProjectReference to
# tools/LicenseGenerator.Core):
#
#   docker build -f backend/Dockerfile -t regkasse-api:local .
#
# Runtime secrets are injected via environment variables (never bake JWT/DB
# passwords into the image). See backend/README.md § Docker and CONFIGURATION.md.
# -----------------------------------------------------------------------------

ARG DOTNET_VERSION=10.0
ARG RID=linux-x64

# --- Restore (cached when only .csproj files change) --------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
ARG RID
WORKDIR /src

COPY tools/LicenseGenerator.Core/LicenseGenerator.Core.csproj tools/LicenseGenerator.Core/
COPY backend/KasseAPI_Final.csproj backend/

RUN dotnet restore backend/KasseAPI_Final.csproj -r ${RID}

# --- Publish (Release, self-contained) ----------------------------------------
FROM restore AS publish
ARG RID
WORKDIR /src

COPY tools/LicenseGenerator.Core/ tools/LicenseGenerator.Core/
COPY backend/ backend/

# Tracked templates only (real appsettings*.json are gitignored and dockerignored).
# Publish needs the conventional file names under the project directory.
RUN cp backend/appsettings.example.json backend/appsettings.json \
 && cp backend/appsettings.Production.example.json backend/appsettings.Production.json \
 && cp backend/appsettings.Development.example.json backend/appsettings.Development.json

RUN dotnet publish backend/KasseAPI_Final.csproj \
    -c Release \
    -r ${RID} \
    --self-contained true \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=true \
    /p:PublishTrimmed=false

# --- Runtime ------------------------------------------------------------------
# Keep aspnet base (not bare runtime-deps) for native deps used by QuestPDF /
# ImageSharp (fonts, libgdi-related libs on Debian).
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

# curl for HEALTHCHECK — clean apt lists (Debian equivalent of apk --no-cache)
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends --no-install-suggests curl \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Non-root (uid provided by the official aspnet image)
USER $APP_UID

COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=45s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/api/health/live || exit 1

ENTRYPOINT ["./KasseAPI_Final"]
