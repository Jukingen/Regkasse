# Regkasse — Comprehensive Project Documentation

> **Audience:** developers, operators, Super Admins  
> **Status:** Evidence-based snapshot as of **2026-07-24**  
> **Not legal advice.** RKSV/FinanzOnline compliance remains operator responsibility.

**Related sources of truth**

| Doc | Role |
|-----|------|
| [`../AGENTS.md`](../AGENTS.md) | Always-applied agent / engineering rules |
| [`../REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) | Project brief for medium/large tasks |
| [`../ai/`](../ai/README.md) | Domain contracts & DO NOT TOUCH |
| [`../README.md`](../README.md) | Monorepo quick start |
| [`CHANGELOG_RECENT.md`](CHANGELOG_RECENT.md) | Recent engineering waves |
| [`ops-known-gaps.md`](ops-known-gaps.md) | Known backup/ops gaps |

---

## Table of contents

1. [Project overview](#1-project-overview)
2. [Improvement summary](#2-improvement-summary)
3. [Detailed improvements by category](#3-detailed-improvements-by-category)
4. [Codebase changes](#4-codebase-changes)
5. [Configuration changes](#5-configuration-changes)
6. [Dependency changes](#6-dependency-changes)
7. [Testing improvements](#7-testing-improvements)
8. [Known issues & TODO](#8-known-issues--todo)
9. [Deployment checklist](#9-deployment-checklist)
10. [Documentation updates](#10-documentation-updates)
11. [Migration notes](#11-migration-notes)
12. [Security notes](#12-security-notes)
13. [Performance impact](#13-performance-impact)
14. [Breaking changes](#14-breaking-changes)

---

## 1. Project overview

### 1.1 Name and description

**Regkasse** is a full-stack, multi-tenant, **Austrian RKSV-oriented** POS platform (Registrierkassen):

- Cashier **POS** (cart → payment → receipt → Tagesabschluss)
- **Admin (FA)** for Mandanten-Admin and Super Admin
- Shared **tenant websites** / online-order intake (non-fiscal)
- ASP.NET Core **API** with TSE/RKSV signing, FinanzOnline outbox, backup/DR, licensing

**Production hosts (single POS UI)**

| Surface | URL |
|---------|-----|
| POS | `https://pos.regkasse.at` |
| Admin (FA) | `https://admin.regkasse.at` |
| API | `https://api.regkasse.at` |

Local defaults: POS `:8081`, FA `:3000`, Sites `:3001`, API `:5184`.  
Detail: [`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md), [`MULTI_TENANT.md`](MULTI_TENANT.md).

### 1.2 Repository structure

| Package | Path | Purpose |
|---------|------|---------|
| API | `backend/` | ASP.NET Core 10 — auth, payments, RKSV/TSE, FinanzOnline, backup, billing, OpenAPI |
| POS | `frontend/` | Expo SDK 56 + React Native — cashier UI (**German de-DE**) |
| Admin | `frontend-admin/` | Next.js 16 + Ant Design 6 — Mandanten + Super Admin (**i18n de/en/tr**) |
| Sites | `frontend-sites/` | Next.js storefronts `/[slug]`, online orders (non-fiscal) |
| Localization | `localization/` | Shared i18n validate/import/export + CI budgets |
| Scripts | `scripts/` | OpenAPI verify, seeds, git hooks, helpers |
| Tools | `tools/` | License generator |
| TestSprite | `testsprite/` | API/E2E specs + CI smoke |
| Docs | `docs/` | Operator/developer documentation (this file) |
| AI contracts | `ai/` | Implementation guardrails |
| CI | `.github/workflows/` | Path-filtered workflows |

Orchestration: **npm workspaces** at repo root (not Turborepo). See [`../package.json`](../package.json), [`../CONTRIBUTING.md`](../CONTRIBUTING.md).

### 1.3 Technology stack (pinned / declared)

| Area | Technology | Version (declared) |
|------|------------|--------------------|
| Backend | ASP.NET Core / `net10.0` | **10.0** |
| EF Core / Identity packages | NuGet | **10.0.10** |
| Database | PostgreSQL | **16+** |
| Admin UI | Next.js | **^16.2.10** |
| Admin UI | React | **^19.2.7** |
| Admin UI | Ant Design | **^6.4.3** |
| Admin data | TanStack Query + Orval | Orval **^6.31.0** |
| POS | Expo | **~56.0.16** |
| POS | React Native | **0.85.3** |
| POS | React | **19.2.3** |
| Sites | Next.js | **^16.2.6** |
| Node | Engine | **≥20** |
| Cache (optional) | Redis | via Docker Compose / `ICacheService` |
| Metrics | prometheus-net | `/metrics` |

Authoritative pin table also lives in [`../AGENTS.md`](../AGENTS.md) § Updated Stack Versions.

### 1.4 API boundaries (do not cross)

| Client | Allowed | Forbidden |
|--------|---------|-----------|
| POS | `/api/pos/*`, `/api/Auth/*`, `/api/Receipts/*` | `/api/admin/*` |
| Admin | `/api/admin/*`, `/api/Auth/*` | `/api/pos/*` |
| Sites / public | `/api/public/*`, `/api/sites/*` | Fiscal POS payment APIs |

Cross-tenant access → **HTTP 404** (not 403). Legacy `/api/Payment|Cart|Product` — soft-deprecated; see [`API_LEGACY_DEPRECATION.md`](API_LEGACY_DEPRECATION.md).

---

## 2. Improvement summary

### 2.1 How to read “~150+ improvements”

There is **no single numbered backlog of 150 tickets** in-repo. Improvements span multiple engineering waves (2026-06 → 2026-07): monorepo DX, backup RBAC, offline systems, digital services, GDPR data management, DEP export, and a large **Super Admin TSE operations** surface.

A practical count of **discrete deliverables** (new APIs, FA pages, migrations, middleware, CI gates, docs) across those waves is **on the order of 100–150+** items depending on granularity. This document inventories what **exists in the codebase**, not a marketing checklist.

### 2.2 Categories

| Category | Status in repo | Highlights |
|----------|----------------|------------|
| Performance | Partial | TanStack Query caching, optional Redis, Prometheus metrics, path-filtered CI |
| Security | Strong baseline | CSRF, security headers, rate limiting, SuperAdmin 2FA, tenant isolation, GDPR rights |
| UX (FA) | Strong | Ant Design 6, i18n de/en/tr, Access & Roles hub, preferences |
| DX | Strong | npm workspaces, Husky, Orval verify, i18n CI, Docker Compose |
| RKSV/TSE core | Production path | Signing pipeline, DEP export, offline limits, certificate ops |
| TSE Super Admin ops | Large additive wave (2026-07-23) | Health, failover, healing, scaling, analytics, KB, … |
| Monitoring | Prometheus + activity feed | Not OpenTelemetry APM |
| Backup & DR | Mature product surface | Tenant vs System, RBAC, restore boundary |
| Permissions | Mature | Matrix, menu mapping, Access hub |
| Smart / predictive | Diagnostic heuristics | Statistical anomalies; not trained ML |

### 2.3 Timeline (documentation / delivery waves)

| Period | Wave | Primary docs |
|--------|------|----------------|
| 2026-06 | Offline order snapshots + monitoring | [`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md), [`CHANGELOG_RECENT.md`](CHANGELOG_RECENT.md) |
| 2026-07 early | Backup permissions + tenant scoping | [`BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md) |
| 2026-07 | Digital services / online orders / working hours | [`DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md), [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md) |
| 2026-07 | SuperAdmin 2FA, monorepo DX, Orval/i18n CI | [`AUTH_TWO_FACTOR.md`](AUTH_TWO_FACTOR.md), root `README.md` |
| 2026-07-23 | TSE Super Admin ops platform (additive) | This document §3.5 / §4 |
| Ongoing | DEP export F1–F5 complete | [`DEP_EXPORT_DEVELOPMENT.md`](DEP_EXPORT_DEVELOPMENT.md) |

---

## 3. Detailed improvements by category

Legend for checklist items requested in the generation brief:

| Mark | Meaning |
|------|---------|
| ✅ | Implemented and evidenced in repo |
| 🟡 | Partial / limited scope |
| ❌ | Not found / not productized |

### 3.1 Performance improvements

| Item | Status | Evidence / notes |
|------|--------|------------------|
| Turborepo build optimization | ❌ | npm workspaces only — no `turbo.json` |
| React Query caching strategy | ✅ | FA uses TanStack Query; Orval hooks; server state not duplicated in Zustand |
| Virtual scrolling for large lists | 🟡 | Used where Ant Design / feature UIs need it; not a global FA standard |
| Code splitting and lazy loading | 🟡 | Next.js App Router route-level splitting |
| Redis caching | 🟡 | Optional `ICacheService` / Redis in Compose; not universal domain cache |
| Database query optimization | 🟡 | Indexes on tenant-scoped entities; EF filters; continuous work |
| API response time improvements | 🟡 | Prometheus `/metrics`; no dedicated APM latency product |

**Performance impact:** optional Redis reduces repeated reads where wired; TSE ops endpoints are Super Admin / diagnostic and should not sit on the POS hot path.

### 3.2 Security improvements

| Item | Status | Evidence |
|------|--------|----------|
| OWASP Top 10 (as a formal program) | 🟡 | Controls exist (authZ, CSRF, headers, validation); not a labeled OWASP checklist project |
| CSP / XSS / CSRF | ✅ | `CsrfMiddleware`; `SecurityHeadersMiddleware`; FA avoids `dangerouslySetInnerHTML` patterns in standards |
| Rate limiting | ✅ | `RateLimitingMiddleware` + specialized limiters |
| Security headers | ✅ | nosniff, frame deny, referrer, permissions-policy, HSTS outside Dev/Staging |
| GDPR / KVKK-style data rights | ✅ | Tenant data management View/Export/Delete; RKSV retention — AGENTS § Expired license |
| Two-factor authentication | ✅ | SuperAdmin TOTP — [`AUTH_TWO_FACTOR.md`](AUTH_TWO_FACTOR.md) |
| Session management | ✅ | JWT + refresh rotation; username change invalidates sessions |

**Security notes:** see [§12](#12-security-notes).

### 3.3 UX improvements

| Item | Status | Evidence |
|------|--------|----------|
| Ant Design 6 migration | ✅ | antd `^6.4.3`; `useNotify` / `useAntdApp` (no static `message`/`Modal.confirm`) |
| Permission UI redesign | ✅ | Access & Roles hub — `frontend-admin/docs/ACCESS_AND_ROLES_HUB.md` |
| Menu-permission mapping | ✅ | [`PERMISSIONS_MENU_MAPPING.md`](PERMISSIONS_MENU_MAPPING.md) |
| Date/time localization (i18n) | ✅ | FA de/en/tr; `formatLocale` preferences |
| Number/currency formatting | ✅ | Locale-aware formatting helpers in FA |
| RTL support | ❌ | Locales are LTR (de/en/tr); no product RTL mode |
| User preferences page | ✅ | `/api/admin/user/preferences` + FA prefs |
| Download preview dialogs | 🟡 | Present on export/backup surfaces |
| Progress indicators | ✅ | Ant Design patterns + backup/run UIs |

### 3.4 DX (developer experience)

| Item | Status | Evidence |
|------|--------|----------|
| Dev container support | ❌ | No `.devcontainer/` |
| VS Code workspace | 🟡 | `Registrierkasse.code-workspace`, `frontend/workspace.code-workspace` |
| Pre-commit hooks (Husky) | ✅ | `.husky/pre-commit` → OpenAPI client verify |
| Commit message validation | ❌ | No commitlint |
| API client generation automation | ✅ | Orval + `scripts/verify-api-client.mjs` + `api-client-auto-generate.yml` |
| i18n validation in CI | ✅ | `localization-validation.yml` (strict missing; Sites deferred) |
| Test reporting and coverage | 🟡 | `dotnet test`, FA/POS Jest/Vitest where configured; coverage not a hard gate everywhere |
| Swagger UI improvements | ✅ | Swagger + guardrail docs under `backend/docs/` |
| API playground | 🟡 | Swagger UI in Development |

### 3.5 RKSV / TSE improvements

#### Core fiscal path (high risk — change carefully)

| Item | Status | Notes |
|------|--------|-------|
| TSE signing on fiscal receipts | ✅ | Core POS payment path |
| Signature chain validation | ✅ | Sequential BelegNr / gap alerts |
| DEP §7 export | ✅ | F1–F5 — [`DEP_EXPORT_DEVELOPMENT.md`](DEP_EXPORT_DEVELOPMENT.md) |
| Offline TSE intent queue | ✅ | Cap 50 / warn 80% — AGENTS § TSE Offline |
| Offline order snapshots | ✅ | Separate system — [`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md) |
| Certificate lifecycle | ✅ | Expiry / renewal services + FA surfaces |
| Multi-provider (Fiskaly / Epson / Swissbit) | 🟡 | Factory knows vendors; **Fiskaly** production path; Epson/Swissbit **stubs** (`UnsupportedVendorTseProvider`) |
| TSE simulator | ✅ | Dev/diagnostic under tse-management simulator |
| Backup/restore (platform) | ✅ | Tenant vs System — not the same as TSE vendor crypto export |

#### Super Admin TSE operations wave (2026-07-23, largely diagnostic)

These surfaces require `system.critical`. Most are **operational / diagnostic** and must **not** rewrite DEP, certificates, or Startbeleg material unless explicitly documented (failover / healing may change which device signs).

| Feature | API | FA route | Notes |
|---------|-----|----------|-------|
| Fleet health (+ Prometheus text) | `/api/admin/tse/health` | API-focused | Diagnostic |
| Log aggregation | `/api/admin/tse/logs` | API-focused | Diagnostic |
| Failover + cost/predictive helpers | `/api/admin/tse/failover` | `/admin/tse/failover`, `/admin/tse/cost` | Fiscal-adjacent (primary device) |
| Resource pools | `/api/admin/tse/resource-pools` | `/admin/tse/resource-pools` | Ops |
| Incidents | `/api/admin/tse/incidents` | `/admin/tse/incidents` | Ops |
| SLA | `/api/admin/tse/sla` | `/admin/tse/sla` | Diagnostic |
| Capacity planning | `/api/admin/tse/capacity` | `/admin/tse/capacity` | Diagnostic |
| Auto-scaling | `/api/admin/tse/auto-scaling` | `/admin/tse/auto-scaling` | Soft recommend; Dev-only soft provision |
| Auto-healing | `/api/admin/tse/auto-healing` | `/admin/tse/auto-healing` | Re-probe / clear error; failover opt-in |
| API gateway config | `/api/admin/tse/api-gateway` | `/admin/tse/api-gateway` | Does not route fiscal Sign |
| Anomaly detection | `/api/admin/tse/anomalies` | `/admin/tse/anomalies` | **Statistical**, not trained ML |
| Analytics / predictive | `/api/admin/tse/analytics` | `/admin/tse/analytics` | Heuristic / diagnostic |
| User analytics | `/api/admin/tse/user-analytics` | `/admin/tse/user-analytics` | Engagement-style |
| Compliance packaging | `/api/admin/tse/compliance` | `/admin/tse/compliance` | Reporting |
| Sustainability | `/api/admin/tse/sustainability` | `/admin/tse/sustainability` | Indicative CO₂e/kWh |
| Recommendations | `/api/admin/tse/recommendations` | `/admin/tse/recommendations` | Workflow markers |
| Zero-downtime update tracking | `/api/admin/tse/updates` | `/admin/tse/updates` | Catalog/policy; not live cloud TSS |
| Disaster recovery runbooks | `/api/admin/tse/disaster-recovery` | `/admin/tse/disaster-recovery` | Simulation drills |
| Webhooks | `/api/admin/tse/webhooks` | `/admin/tse/webhooks` | Outbound ops alerts |
| Blockchain anchors | `/api/admin/tse/blockchain` | `/admin/tse/blockchain` | Simulated ledger — **not RKSV proof** |
| Training | `/api/admin/tse/training` | `/admin/tse/training` | Dev failure drills |
| Knowledge base / FAQ | `/api/admin/tse/knowledge` | `/admin/tse/knowledge` | Seeded operational docs |
| Developer tools | `/api/admin/tse/developer-tools` | `/admin/tse/developer-tools` | Debug |
| Offline queue (legacy intents) | `/api/admin/tse/offline-queue` | `/admin/tse/offline-transactions` | Fiscal-adjacent |
| TSE management / provision | `/api/admin/tse-management` | Management surfaces | Fiscal-adjacent |

**Migrations (additive, same-day wave examples):**  
`20260723290000_AddTseResourcePools` … `20260723410000_AddTseKnowledgeBase` under `backend/Migrations/`.

### 3.6 Monitoring & observability

| Item | Status | Evidence |
|------|--------|----------|
| APM OpenTelemetry | ❌ | Not registered in backend |
| Health check API | ✅ | `/health`, `/health/live`, `/health/ready`, `/api/health*` |
| Performance monitoring | 🟡 | Prometheus `/metrics`; FA monitoring docs |
| Log aggregation (TSE ops) | 🟡 | Admin TSE logs API — not a full ELK product |
| Alerting | ✅ | Activity feed + optional email/webhooks |
| Metrics dashboard | 🟡 | Prometheus scrape + FA monitoring pages |

Detail: [`release/CORE_METRICS_PROMETHEUS.md`](release/CORE_METRICS_PROMETHEUS.md), `frontend-admin/docs/MONITORING.md`.

### 3.7 Backup & disaster recovery

| Item | Status | Notes |
|------|--------|-------|
| Automated backup verification | 🟡 | Verification states + restore drills; see known gaps |
| DR runbook (platform) | ✅ | Docs + FA `/backup` |
| TSE DR runbooks | ✅ | `/admin/tse/disaster-recovery` (simulation-oriented) |
| RTO/RPO tracking | 🟡 | Design goals in [`ops-known-gaps.md`](ops-known-gaps.md); infra-dependent |
| TSE backup/restore | 🟡 | TSE backup records / management APIs; vendor crypto still a gap |
| Grace period for critical ops | ✅ | License grace / archive lifecycle |

Hub: [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md).

### 3.8 Permission system

| Item | Status | Notes |
|------|--------|-------|
| Role hierarchy / defaults | ✅ | `RolePermissionMatrix`, Roles.cs |
| Time-bound permissions | 🟡 | Not a first-class global product feature |
| Permission request workflow | 🟡 | Digital **service** requests exist; not a general permission-request engine |
| Permission packages/groups | ✅ | Menu permission groups / Access hub |
| Audit log for permissions | ✅ | Audit + activity events for sensitive changes |
| Menu-permission mapping | ✅ | [`PERMISSIONS_MENU_MAPPING.md`](PERMISSIONS_MENU_MAPPING.md) |
| Impact analysis | 🟡 | Impact simulator / FA tooling where present |

### 3.9 File & export management

| Item | Status | Notes |
|------|--------|-------|
| Standardized naming | 🟡 | Per export type (DEP, data export ZIP, backup artifacts) |
| Download history | 🟡 | Backup runs / data-rights requests |
| Batch download | 🟡 | Limited to specific admin flows |
| File preview | 🟡 | Where UI supports it |
| Download security rules | ✅ | Opaque tokens, permission gates, tenant scoping |
| Export templates | 🟡 | Receipt templates / report PDFs |

### 3.10 Maintenance & operations

| Item | Status | Notes |
|------|--------|-------|
| Maintenance mode | ✅ | Super Admin maintenance surfaces / notifications |
| Maintenance notifications | ✅ | Migration + FA |
| Read-only mode | ✅ | License locked / archived FA write gating |
| Zero-downtime updates (TSE ops tracker) | 🟡 | Policy/history tracking — not automatic cloud TSS rollout |
| Auto-healing | ✅ | `/admin/tse/auto-healing` |

### 3.11 Smart features

| Item | Status | Notes |
|------|--------|-------|
| AI/ML anomaly detection | 🟡 | **Statistical baseline** only (`TseAnomalyDetectionService`) |
| Smart recommendations | ✅ | TSE recommendations (apply = workflow marker) |
| Predictive analytics | 🟡 | Heuristic risk / forecasts — diagnostic |
| Auto-scaling | ✅ | Soft recommendations; Dev soft-provision optional |
| Capacity planning | ✅ | Utilization-oriented |
| Cost optimization | ✅ | Cost report / anomalies on failover surface |

---

## 4. Codebase changes

### 4.1 Backend (BE)

#### New / extended TSE service layer

Under `backend/Services/Tse/`:

- Fleet health, metrics, log aggregation  
- Failover (+ notifications)  
- Resource pools, incidents, SLA, capacity, auto-scaling, auto-healing  
- API gateway, anomaly detection, predictive/reporting/user analytics  
- Compliance reports, sustainability, recommendations, updates  
- Disaster recovery, webhooks, blockchain anchors, training, knowledge base  
- Developer tools, simulator, provisioning/backup helpers  
- `ITenantTsePortalService` for mandant portal status  

DI registration: `backend/ApplicationHost.cs` (`AddScoped<ITse…>`).

#### Controllers (Super Admin)

26 controllers matching `backend/Controllers/AdminTse*.cs` (see §3.5 table).  
Permission gate: typically `AppPermissions.SystemCritical` / `system.critical`.

#### Database (additive migrations)

Examples from the 2026-07-23 wave:

| Migration | Tables / purpose |
|-----------|------------------|
| `…AddTseResourcePools` | Pools / assignments / rules |
| `…AddTseIncidents` | Incidents / logs / actions |
| `…AddTseDrRunbooks` | DR runbooks / steps |
| `…AddTseAutoScaling` | Scaling policy / history |
| `…AddTseApiGateway` | Gateway config / endpoints |
| `…AddTseAnomalies` | Anomaly records |
| `…AddTseWebhooks` | Webhook registrations / deliveries |
| `…AddTseBlockchainAnchors` | Anchor / ledger state |
| `…AddTseTrainingProgress` | Training progress |
| `…AddTseRecommendations` | Recommendation rows |
| `…AddTseUpdates` | Update state / history |
| `…AddTseAutoHealing` | Healing config / rules / history |
| `…AddTseKnowledgeBase` | Articles / feedback |

Earlier same-day: TSE backups, failover support, device health samples, certificate renewal columns.

#### DTOs / models

- DTOs: `backend/DTOs/Tse*Dtos.cs`  
- Models: `backend/Models/Tse*.cs`, `ActivityEventType` extensions for ops events  
- Options: `TseOptions` rates/thresholds  

### 4.2 Frontend Admin (FA)

#### New Super Admin TSE routes

Under `frontend-admin/src/app/(protected)/admin/tse/`:

`analytics`, `anomalies`, `api-gateway`, `auto-healing`, `auto-scaling`, `blockchain`, `capacity`, `compliance`, `cost`, `developer-tools`, `disaster-recovery`, `failover`, `incidents`, `knowledge`, `offline-transactions`, `recommendations`, `resource-pools`, `sla`, `sustainability`, `training`, `updates`, `user-analytics`, `webhooks`, …

#### Supporting FA wiring

- Feature APIs: `frontend-admin/src/features/tse-*`  
- i18n namespaces: `tseAutoHealing`, `tseKnowledge`, `tseAutoScaling`, … (de/en/tr)  
- Nav / sidebar: `adminSidebarRegistry.ts`, `adminSidebarNavigation.ts`  
- Permissions: `routePermissions.ts` → `SYSTEM_CRITICAL`  
- Localization manifest: `localization/namespace-manifest.json`

#### Broader FA improvements (prior waves)

- Access & Roles hub  
- Backup hub (`/backup`, costs, compliance)  
- Digital services / online orders  
- Offline orders admin  
- Data management (GDPR)  
- Ant Design 6 notification patterns  

### 4.3 POS Frontend (FE)

| Area | Changes |
|------|---------|
| Offline order snapshots | Queue, banner, sync, storage — separate from legacy TSE intents |
| Working hours | Display / Tagesabschluss reminders only — **never** gates POS operations |
| Production host | Single POS UI + JWT tenant |
| Language | UI remains **German (de-DE)** |

Index: [`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md), [`WORKING_HOURS.md`](WORKING_HOURS.md).

---

## 5. Configuration changes

### 5.1 Environment / appsettings (representative)

Backend (see `backend/appsettings.example.json`, `backend/README.md`):

```text
ConnectionStrings__DefaultConnection=Host=localhost;Database=kasse_db;…
JwtSettings__SecretKey=…                 # min 32 chars
Security__Csrf__Enabled=true|false
TwoFactorAuth__Enabled=true|false
TwoFactorAuth__BypassInDevelopment=true  # Dev only
FinanzOnline__Mode=Simulation|Production
Tse__…                                   # offline caps, failover thresholds, sustainability rates
Backup__…                                # schedule, retention, smart retention flags
```

Frontend Admin:

```text
NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
```

POS:

```text
EXPO_PUBLIC_API_BASE_URL=http://…:5184/api
EXPO_PUBLIC_DEV_TENANT_ID=dev
```

### 5.2 Docker

Root `docker-compose.yml`: PostgreSQL 16, Redis, API, Admin (POS profile optional).  
See [`../DEVELOPMENT.md`](../DEVELOPMENT.md).

### 5.3 CI/CD

Path-filtered workflows for backend, FA, POS, Sites; NuGet/node caches; localization hard gate; API client alignment.  
Inventory: [`.github/workflows/README.md`](../.github/workflows/README.md).

### 5.4 Monitoring

- Scrape Prometheus: `/metrics`  
- Liveness/readiness: `/health/live`, `/health/ready`  
- Activity SSE/email/webhooks for critical ops events  

---

## 6. Dependency changes

### 6.1 Notable stack upgrades (pinned)

| Dependency | Direction |
|------------|-----------|
| .NET / EF | **10.0 / 10.0.10** |
| Next.js (FA) | **16.2.x** |
| React 19 | FA + POS + Sites |
| Ant Design | **6.4.x** |
| Expo | **SDK 56** |
| Husky | `^9.1.7` (root) |
| Orval | `^6.31.0` (FA) |
| prometheus-net | Metrics exposition |

### 6.2 Not introduced

- Turborepo  
- OpenTelemetry SDK (as primary APM)  
- Commitlint  

Exact package diffs: consult `package-lock.json` / NuGet restore for a given release tag.

---

## 7. Testing improvements

| Layer | What exists |
|-------|-------------|
| Backend unit | Large `KasseAPI_Final.Tests` suite — TSE ops services have dedicated tests (e.g. `TseAutoHealingServiceTests`, `TseKnowledgeBaseServiceTests`, scaling/anomaly/…) |
| Backend integration | PostgreSQL integration workflows; backup trigger/download tests |
| FA | Jest/Vitest + contract tests (working hours non-gating, permissions fixtures) |
| POS | Unit tests for offline/working-hours contracts |
| E2E / smoke | TestSprite + CI smoke; beta smoke checklists under `docs/` |
| Fiscal verification | DEP Prüftool script `scripts/verify-rksv-dep-export.ps1` |

### Suggested local validation

```bash
# From repo root
npm run verify:api-client
dotnet test backend/KasseAPI_Final.sln
npm run test:admin
npm run test:pos

# Localization (FA)
node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error
```

Filter examples:

```bash
cd backend
dotnet test --filter "TseAutoHealingServiceTests|TseKnowledgeBaseServiceTests|RksvDepExportServiceTests"
```

---

## 8. Known issues & TODO

### 8.1 Known gaps (documented)

From [`ops-known-gaps.md`](ops-known-gaps.md) and product docs:

- Full WAL/PITR / base-backup style RPO is **infrastructure**, not app-only  
- Per-tenant **physical** dump files still limited (access scoping ≠ separate dumps)  
- TSE **vendor** crypto backup coverage incomplete  
- Epson / Swissbit providers are **stubs**  
- TSE anomaly / predictive features are **heuristic**, not certified ML  
- Blockchain anchors are **not** RKSV legal proof  
- Sites i18n catalogs **deferred** in `localization/namespace-manifest.json`  

### 8.2 Future improvements (suggested)

1. Wire FA pages for fleet health/logs if API-only today is insufficient for ops  
2. Harden auto-healing defaults (keep `AllowAutoFailover=false` in production)  
3. Promote statistical anomaly thresholds via config + operator runbooks  
4. Complete vendor provider adapters beyond Fiskaly when certified  
5. OpenTelemetry (optional) alongside Prometheus if APM vendors require it  
6. Devcontainer for one-command onboarding  
7. Commitlint if conventional commits become a hard policy  

### 8.3 Planned product features

Track product roadmap outside this file; engineering waves land via `docs/CHANGELOG*.md` and PRs.

---

## 9. Deployment checklist

### 9.1 Prerequisites

- [ ] Node 20+, npm workspaces  
- [ ] .NET SDK 10.x  
- [ ] PostgreSQL 16+  
- [ ] Optional Redis  
- [ ] Secrets: JWT, DB, FinanzOnline, TSE provider credentials  

### 9.2 Environment setup

```bash
npm install
# Configure backend user-secrets / Production appsettings (never commit secrets)
dotnet ef database update --project backend
npm run dev   # or docker compose up --build
```

Production hosts and cutover: [`../DEPLOYMENT.md`](../DEPLOYMENT.md), FinanzOnline checklists under `docs/`.

### 9.3 Migration steps (TSE ops wave)

1. Take a **System** backup before applying new migrations in shared environments.  
2. Apply EF migrations including `20260723*` TSE additive tables.  
3. Redeploy API so new `AdminTse*` controllers and DI registrations load.  
4. Redeploy FA for new `/admin/tse/*` routes and i18n namespaces.  
5. Confirm Super Admin `system.critical` still required for TSE ops pages.  

### 9.4 Validation steps

- [ ] `/health/ready` OK  
- [ ] Login + SuperAdmin 2FA behavior matches env  
- [ ] Sample DEP export still works (fiscal regression)  
- [ ] POS payment smoke (non-destructive test tenant)  
- [ ] Open one diagnostic TSE page (e.g. `/admin/tse/knowledge`)  
- [ ] Confirm auto-healing **disabled** and **AllowAutoFailover=false** unless intentionally enabled  
- [ ] Prometheus `/metrics` scrapes if required  

### 9.5 Rollback plan

1. Redeploy previous API/FA artifacts.  
2. **Do not** delete additive migration history casually.  
3. New TSE ops tables can remain unused if code is rolled back (safe additive schema).  
4. Restore from System backup only via **approved restore drill path** — never casual production restore ([`restore-boundary-notes.md`](restore-boundary-notes.md)).  

---

## 10. Documentation updates

| Document | Role after this wave |
|----------|----------------------|
| **This file** | Comprehensive inventory & onboarding bridge |
| [`../AGENTS.md`](../AGENTS.md) | Always-on rules (stack, tenancy, fiscal guardrails) |
| [`../REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) | Medium/large task brief |
| [`../ai/07_DO_NOT_TOUCH.md`](../ai/07_DO_NOT_TOUCH.md) | High-risk flows |
| [`API_CONTRACTS.md`](API_CONTRACTS.md) | API contracts hub |
| [`CHANGELOG.md`](CHANGELOG.md) / [`CHANGELOG_RECENT.md`](CHANGELOG_RECENT.md) | Feature waves |
| Backup / offline / DEP / 2FA docs | Domain deep-dives |

When adding a new Super Admin TSE feature, update: controller + service + FA page + i18n (3 locales) + `namespace-manifest.json` + sidebar/nav/`routePermissions` + this inventory table.

---

## 11. Migration notes

**Important for existing deployments**

1. **Additive schema only** — do not edit committed migrations; follow AGENTS database baseline rules.  
2. **TSE ops tables** are platform/tenant-scoped as designed per feature; Super Admin uses `IgnoreQueryFilters` carefully where documented.  
3. **Auto-healing / failover** can change signing device selection — treat as operational risk even though they do not rewrite receipt bytes.  
4. **Legacy Payment/Cart/Product** soft sunset headers → hard remove after documented gates ([`API_LEGACY_DEPRECATION.md`](API_LEGACY_DEPRECATION.md)).  
5. **Offline systems** are two products — do not merge `offline_transactions` and `offline_orders`.  

---

## 12. Security notes

1. Super Admin TSE pages are **`system.critical`** — do not widen to Mandanten-Admin without a threat review.  
2. CSRF enabled in Production; clients must send `X-XSRF-TOKEN`.  
3. Cross-tenant reads must remain **404**.  
4. Never log voucher codes, passwords, or raw card data.  
5. Knowledge base / blockchain / sustainability outputs are **operational**, not legal attestation.  
6. GDPR Delete preserves RKSV-required fiscal rows — see AGENTS § Expired license data management.  

---

## 13. Performance impact

| Change | Expected impact |
|--------|-----------------|
| npm workspace parallel `dev` / path-filtered CI | Faster local + CI feedback |
| Optional Redis | Lower latency for cached admin reads where enabled |
| Prometheus metrics | Small scrape overhead |
| TSE ops analytics/anomaly jobs | CPU/DB on Super Admin actions — keep off POS critical path |
| Auto-healing re-probes | Extra health checks; cooldown + max attempts mitigate thrash |

---

## 14. Breaking changes

| Change | Breaking? | Action |
|--------|-----------|--------|
| TSE Super Admin ops wave (2026-07-23) | **No** (additive APIs/tables/pages) | Deploy migrations + apps |
| Soft-deprecate `/api/Payment|Cart|Product` | **Upcoming** hard remove | Migrate clients before sunset |
| Ant Design 6 static APIs | **Yes for FA code** using `message`/`Modal.confirm` | Use `useNotify` / `useAntdApp` |
| Next.js 16 `proxy.ts` auth boundary | **Yes vs old middleware patterns** | Follow FA auth docs |
| Working hours gating on POS | **Must not break** — POS stays ungated | Contract tests enforce |

---

## Appendix A — Quick command reference

```bash
# Dev
npm run dev
npm run dev:backend | npm run dev:admin | npm run dev:pos | npm run dev:sites

# Generate / verify OpenAPI clients
node scripts/generate-backend-openapi.mjs
cd frontend-admin && npm run generate:api
node scripts/verify-api-client.mjs

# Tests
dotnet test backend/KasseAPI_Final.sln
npm run test:admin
npm run test:pos
```

## Appendix B — High-risk flows (do not casually change)

From [`../AGENTS.md`](../AGENTS.md) / [`../ai/07_DO_NOT_TOUCH.md`](../ai/07_DO_NOT_TOUCH.md):

- Cart → Payment → Receipt → DailyClosing  
- TSE signature chain / special receipts  
- FinanzOnline outbox SOAP  
- Tenant isolation / query filters  
- Voucher ledger  
- Backup restore into production  

---

## Appendix C — Document maintenance

| Field | Value |
|-------|-------|
| Created | 2026-07-24 |
| Location | `docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md` |
| Update when | New Super Admin TSE feature, stack pin change, breaking API sunset, backup/DR policy change |
| Accuracy rule | Prefer “not found / partial” over inventing checklist items |

**End of document.**
