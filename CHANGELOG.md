# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) where release tags are cut.

**Older waves** (detailed narrative):

- [`docs/CHANGELOG.md`](docs/CHANGELOG.md) — digital services / online orders
- [`docs/CHANGELOG_RECENT.md`](docs/CHANGELOG_RECENT.md) — recent engineering waves
- [`docs/CHANGELOG_TENANT_MANAGEMENT.md`](docs/CHANGELOG_TENANT_MANAGEMENT.md) — tenant / license FA

---

## [Unreleased]

### Added

- **Monorepo DX docs:** root [`README.md`](README.md), [`DEVELOPMENT.md`](DEVELOPMENT.md), [`DEPLOYMENT.md`](DEPLOYMENT.md), [`CONTRIBUTING.md`](CONTRIBUTING.md), [`API_CONTRACT.md`](API_CONTRACT.md); package READMEs for `docs/`, `ai/`, `scripts/`, `tools/`, `localization/`, `frontend-sites/`.
- **npm workspaces** at repo root (`backend`, `frontend`, `frontend-admin`, `frontend-sites`, `localization`) with `npm run dev` parallel runner (`scripts/dev-workspaces.mjs`).
- **Husky pre-commit** — staged-package lint/typecheck + OpenAPI/API client verify (`scripts/git-hooks/pre-commit.mjs`); opt-in `HUSKY_RUN_TESTS=1`.
- **API client automation:** `scripts/verify-api-client.mjs` (incl. `--openapi-only`), CI `api-client-alignment.yml` / `api-client-auto-generate.yml`.
- **CI coverage:** frontend POS / sites workflows; backend unit + Postgres integration improvements; reusable failure notify; workflow inventory [`.github/workflows/README.md`](.github/workflows/README.md).
- **TestSprite runners:** `testsprite/validate-specs.mjs` (YAML ↔ `swagger.json` CI gate), `testsprite/run-api-smoke.mjs` (live smoke); suites for offline-orders and sites-public.
- **i18n CI hard gates** for Admin/POS via `localization/`; `frontend-sites` deferred in namespace manifest.
- **SuperAdmin 2FA (TOTP):** Production authenticator after password; Development bypass — [`docs/AUTH_TWO_FACTOR.md`](docs/AUTH_TWO_FACTOR.md).
- **Digital services & online orders:** website/app generation, Manager preview/requests, non-fiscal online-order inbox — [`docs/DIGITAL_SERVICES.md`](docs/DIGITAL_SERVICES.md), [`docs/ONLINE_ORDERS.md`](docs/ONLINE_ORDERS.md).
- **Username-based login:** `loginIdentifier` on `POST /api/Auth/login` (email or username).
- **Auto-generated usernames:** Quick Create / admin create when `userName` omitted (`{rolePrefix}{n}`).
- **Username UX (FA):** success modals with username, email, one-time password + copy helpers.
- **License tools docs:** [`tools/README.md`](tools/README.md); portable Redis notes under `tools/redis/`.

### Changed

- **Documentation accuracy:** Single POS UI hosts (`pos` / `admin` / `api`.regkasse.at); JWT tenant; Dev-only `X-Tenant-Id` — updates across `docs/`, `ai/`, offline deploy, impersonation, onboarding.
- **Backup FA docs:** hub `/backup` (+ costs/compliance); Mandanten-Admin `backup.manage`; auto-cleanup audit `BACKUP_AUTO_DELETED`.
- **`POST /api/Auth/login`:** prefers `loginIdentifier`; resolves email then username (`IdentityLoginLookup`).
- **FA / POS login clients:** send `loginIdentifier` (legacy `email` mirrored where needed).
- **Tenant user create:** `UserName` no longer forced equal to email; optional `userName` on create/quick DTOs; responses include `userName`.
- **AGENTS / onboarding:** stack pins (.NET/EF 10.0.10, Next 16, Expo 56), workspaces, Sites, backup extras.
- **TestSprite CI:** validates OpenAPI alignment instead of non-existent `testsprite-cli`; live smoke on `workflow_dispatch`.
- **`tools/i18n`:** thin wrappers over `localization/scripts/*` (no parallel validator).

### Fixed

- **AI contracts:** removed false “Customer not `ITenantEntity`” claim; Production tenancy framed as JWT on reserved hosts (not subdomain-only).
- **Auth docs:** invalid login expected status **401** (not 400) in TestSprite / API contract summaries.
- **Seed / smoke scripts:** `seed-test-data.mjs` uses `loginIdentifier` and clearer API-unreachable errors; FA E2E uses Dev tenant header `dev`.
- **TestSprite POS smoke script:** portable `npx @testsprite/testsprite-mcp` (no machine-specific npm-cache path).

### Deprecated

- **`email`** on login request bodies — still accepted when `loginIdentifier` is empty; new clients must use **`loginIdentifier`** (OpenAPI marks `email` deprecated).
- **Legacy slug POS entry** (`{slug}.regkasse.at` as primary POS) — prefer `pos.regkasse.at` + JWT; FA impersonation slug handoff treated as technical debt vs shared Admin host.
- **Legacy API families** `/api/Payment`, `/api/Cart`, `/api/Product` — no new features; see [`docs/API_LEGACY_DEPRECATION.md`](docs/API_LEGACY_DEPRECATION.md).

### Removed

- Obsolete scripts: `migrate_i18n.js`, `patch-swagger-backup-dr.cjs`, `parse-demo-menu-html.*`, `ci-smoke-test.sh` (legacy Cart smoke).
- Empty `tools/LicenseTools.slnx`; duplicate TestSprite YAML (`multi-tenant.yml`, `backup-restore.yml` merged into remaining suites).
- Broken standalone `tools/i18n/projects.mjs` / legacy validate implementation (replaced by localization wrappers).

---

## Older notes (pre–Keep a Changelog consolidation)

The following narrative was previously nested under `[Unreleased]` without standard categories. Behaviour is still current; prefer the sections above for new entries.

### Backend (username / login wave)

- **`UniqueUsernameGenerator`** and **`IUserCreationService`** / **`UserCreationService`**.
- **`TenantUserService`**, **`AdminUsersController.Create`**, **`QuickUserGeneratorService`** for optional/auto `userName`.
- **`LoginModel`** + **`AuthController`** username/email lookup.
- Quick Create email: `{role}_{random6}@{tenantSlug}.regkasse.at`.

### Frontend-Admin (username / login wave)

- Schnell anlegen preview; **`QuickUserSuccessModal`**; i18n de/en/tr for login identifier.
- Docs: FA README, onboarding Authentication section.

### Frontend-POS (username / login wave)

- **`loginIdentifier`** persistence; **`authService.buildLoginPayload`**; i18n placeholders.

---

## [0.0.0] — placeholder

No dated SemVer release tag has been published in this changelog yet. When cutting a release, move `[Unreleased]` items into `## [x.y.z] - YYYY-MM-DD` and keep an empty `[Unreleased]` stub.
