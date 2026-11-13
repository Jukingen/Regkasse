# Regkasse monorepo — common developer tasks (https://github.com/casey/just)
# Install: winget install Casey.Just   |   brew install just   |   cargo install just
# Usage:   just <recipe>               |   just --list

# Windows: run recipes in PowerShell (npm/docker on PATH)
set windows-shell := ["powershell.exe", "-NoLogo", "-Command"]

# Default: list recipes
default:
    @just --list

# Run API + POS + Admin + Sites in development
dev:
    npm run dev

# Build all projects (workspaces --if-present)
build:
    npm run build

# Run all tests (workspaces --if-present)
test:
    npm run test

# Run all linters (workspaces --if-present)
lint:
    npm run lint

# Typecheck Admin, POS, Sites
typecheck:
    npm run typecheck

# Clean build artifacts (bin/obj/.next/dist/.expo)
clean:
    node scripts/clean-artifacts.mjs

# Start full Dev stack via Docker Compose (detached; Soft TSE override)
docker-up:
    docker compose up --build -d

# Start Postgres + Redis only (host runs API/frontends)
docker-up-dev:
    docker compose -f docker-compose.dev.yml up -d

# Production-oriented Compose (requires .env.production from .env.production.example)
docker-up-prod:
    docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build

# Stop Docker Compose stacks (keep volumes)
docker-down:
    docker compose down
    docker compose -f docker-compose.dev.yml down

# Start Compose including POS static web (--profile pos)
docker-up-pos:
    docker compose --profile pos up --build -d

# Tail Docker Compose logs
docker-logs:
    docker compose logs -f

# Verify OpenAPI / generated Admin API client
verify-api:
    npm run verify:api-client

# Run localization CI checks
i18n:
    npm run i18n:ci
