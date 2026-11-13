# Regkasse monorepo — common developer tasks
# Requires: GNU Make + Node 20+ (+ Docker for docker-* targets)
# Windows: install Make via Chocolatey (`choco install make`) or use Git Bash;
#          or prefer `just` with the companion Justfile (`winget install Casey.Just`).
#
# Usage: make <target>
#        make help

.DEFAULT_GOAL := help

.PHONY: help dev build test lint typecheck clean \
	docker-up docker-down docker-up-dev docker-up-prod docker-up-pos docker-logs \
	verify-api i18n

help: ## Show this help
	@echo Regkasse Make targets:
	@echo   make dev           - Run all services (npm run dev)
	@echo   make build         - Build all workspaces
	@echo   make test          - Run all workspace tests
	@echo   make lint          - Run all workspace linters
	@echo   make typecheck     - Typecheck Admin / POS / Sites
	@echo   make clean         - Remove build artifacts
	@echo   make docker-up     - Full Dev Compose (+ Soft TSE override)
	@echo   make docker-up-dev - Start Postgres + Redis only (host apps)
	@echo   make docker-up-prod - Production-oriented Compose (.env.production)
	@echo   make docker-down   - Stop Docker Compose
	@echo   make docker-up-pos - Compose + optional POS web profile
	@echo   make docker-logs   - Follow Compose logs
	@echo   make verify-api    - OpenAPI / Orval client verify
	@echo   make i18n          - Localization CI gate

dev: ## Run API + POS + Admin + Sites in development
	npm run dev

build: ## Build all projects (workspaces --if-present)
	npm run build

test: ## Run all tests (workspaces --if-present)
	npm run test

lint: ## Run all linters (workspaces --if-present)
	npm run lint

typecheck: ## Typecheck Admin, POS, Sites
	npm run typecheck

clean: ## Clean build artifacts (bin/obj/.next/dist/.expo)
	node scripts/clean-artifacts.mjs

docker-up: ## Start full Dev stack via Docker Compose (detached; Soft TSE override)
	docker compose up --build -d

docker-up-dev: ## Start Postgres + Redis only (for host npm/dotnet)
	docker compose -f docker-compose.dev.yml up -d

docker-up-prod: ## Production-oriented Compose (requires .env.production)
	docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build

docker-down: ## Stop Docker Compose stacks (keep volumes)
	-docker compose down
	-docker compose -f docker-compose.dev.yml down
	-docker compose -f docker-compose.prod.yml --env-file .env.production down

docker-up-pos: ## Start Compose including POS static web (--profile pos)
	docker compose --profile pos up --build -d

docker-logs: ## Tail Docker Compose logs
	docker compose logs -f

verify-api: ## Verify OpenAPI / generated Admin API client
	npm run verify:api-client

i18n: ## Run localization CI checks
	npm run i18n:ci
