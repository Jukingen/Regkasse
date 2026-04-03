# Repository File Map (current)

## Backend (`backend/`)
- Controllers: `backend/Controllers/`
- Services: `backend/Services/`
- Authorization: `backend/Authorization/`
- EF context: `backend/Data/AppDbContext.cs`
- Migrations: `backend/Migrations/`
- OpenAPI contract: `backend/swagger.json`
- Backend tests: `backend/KasseAPI_Final.Tests/`

## POS Frontend (`frontend/`)
- Expo Router app: `frontend/app/`
- API services: `frontend/services/api/`
- Contexts: `frontend/contexts/`
- POS tests: `frontend/__tests__/`

## Admin Frontend (`frontend-admin/`)
- Next App Router pages: `frontend-admin/src/app/`
- Generated API client: `frontend-admin/src/api/generated/`
- Admin API boundary helpers: `frontend-admin/src/api/admin/`
- Axios mutator: `frontend-admin/src/lib/axios.ts`
- Orval config/transformer: `frontend-admin/orval.config.ts`, `frontend-admin/scripts/orval-strip-legacy-paths.cjs`

## CI & verification
- Workflows: `.github/workflows/*.yml`
- OpenAPI/Orval checks: `scripts/verify-api-client.mjs`, `scripts/validate-critical-openapi-paths.mjs`
- Localization checks: `localization/scripts/*.mjs`
