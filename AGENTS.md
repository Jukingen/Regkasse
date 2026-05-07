# AGENTS.md

## Purpose
This repository is a POS monorepo. Prefer safe, incremental improvements over broad rewrites. Follow real package boundaries, preserve current architecture unless explicitly asked otherwise, and make the smallest safe change that satisfies the task.

## Language rules
Follow these language rules strictly:

- Code identifiers must be in English.
- Code comments must be in English.
- POS user interface texts must remain in German.
- Do not translate POS UI text into English or Turkish.
- When explaining plans, changes, or reviews in the IDE, respond in Turkish.

## Working style
- Prefer minimal, targeted changes over broad refactors.
- Preserve existing architecture and naming conventions unless explicitly asked for restructuring.
- Before editing, inspect nearby files and follow local patterns.
- Do not invent commands, package relationships, or framework conventions.
- State uncertainty explicitly when repo evidence is missing.
- Do not mix unrelated refactors into feature work.
- Prefer controlled evolution, small reversible steps, and behavior-preserving refactors.
- Prefer updating existing code paths over introducing parallel implementations.

## Repo map
- `backend/`
  - Main ASP.NET Core API.
  - Owns auth, authorization, domain logic, persistence, fiscal/TSE/RKSV behavior, reporting, and OpenAPI contract.
- `frontend/`
  - Mobile POS client.
  - Expo Router + React Native + TypeScript.
- `frontend-admin/`
  - Admin panel.
  - Next.js + TypeScript + Ant Design + TanStack Query + mixed generated/manual/legacy API boundaries.
- `localization/`
  - Shared i18n import/export/validation tooling.
- `scripts/`
  - Cross-repo validation and consistency scripts.
- `.github/workflows/`
  - CI source of executable truth.
- `docs/`
  - Human documentation and reference material.
- `ai/`
  - Internal implementation and guardrail docs that must be read before medium/high-risk work.

## Source of truth
When deciding how to work, trust these in order:

1. Actual implementation in the nearest relevant files
2. Package-level config files
3. Root config files
4. CI workflows
5. README, `docs/`, and **`REGKASSE_AI_ONBOARDING.md`** (AI/project brief at repo root)
6. `ai/` guidance docs for domain-specific safety and implementation constraints

If they conflict, follow the most local and executable truth.

## Read before changing code
Before making changes:

1. For project-wide fiscal/RKSV/POS context, read **`REGKASSE_AI_ONBOARDING.md`**; then read the relevant docs under `/ai`
2. Respect compliance and fiscal/TSE/RKSV rules
3. Follow existing repo patterns
4. Preserve backward compatibility unless explicitly told otherwise

For medium or large changes, always provide:
- a short implementation plan
- affected files
- main risks
- backward compatibility impact
- a test strategy

## AI docs routing hints
Use `/ai` docs selectively based on the task:

- Backend/API/auth/contract work:
  - read the backend/API-related docs in `/ai`
- Database/entity/migration/persistence work:
  - read the database contract and persistence-related docs in `/ai`
- Compliance, fiscal, TSE, RKSV, audit, receipt, daily closing work:
  - read the compliance and protected-area docs in `/ai`
- Admin API integration work:
  - read API boundary and admin-related docs in `/ai`
- If unsure:
  - read the closest matching `/ai` docs first and avoid assumptions

## Directory hints
Before editing in each area, inspect these first:

- `backend/`
  - relevant controllers
  - services / use-cases
  - DTOs
  - validators
  - EF entities and mappings
  - migrations
  - impacted tests
- `frontend/`
  - relevant screens
  - hooks
  - contexts
  - navigation flow
  - API usage
  - impacted tests
- `frontend-admin/`
  - relevant routes/pages
  - feature components
  - hooks and queries
  - generated/manual API usage
  - auth gates
  - impacted tests
  - related i18n keys
- `localization/`
  - validation scripts
  - catalog ownership
  - missing/orphan key rules
  - CI budget or boundary checks

## Rule application model
Apply these rules in this order:

### Always-on baseline
These principles always apply:
- safe incremental changes over rewrites
- compliance and fiscal safety first
- backward compatibility first
- no speculative architecture changes
- explicit risk notes for sensitive flows

### Fiscal compliance (mandatory)
- Check NTP time sync status before treating online fiscal payments as allowed (`NtpSettings` / `NtpTimeSyncStatus`; block when sync failed or offset exceeds configured `MaxAllowedOffsetSeconds`).
- Never queue voucher (Gutschein) payments for offline non-fiscal replay—backend must reject; POS must not enqueue voucher payloads.
- Storno flows must supply **`OriginalReceiptId`** and a **`StornoReason`** where the contract requires them; do not conflate with partial refund.
- DEP-style fiscal export generation/download may require disclaimer acknowledgment: send **`X-Disclaimer-Acknowledged: true`** when `FiscalExportOptions.RequireDisclaimerAcknowledgment` is enabled.

### Context-driven rules
When touching backend or persistence, increase caution around:
- controllers, services, use-cases
- EF Core entities and mappings
- migrations and schema evolution
- auditability and compliance behavior
- API contracts and DTOs

### Path-specific rules
When touching `frontend/**`:
- use Expo / React Native patterns only
- avoid web-only abstractions and browser-specific assumptions
- avoid growing orchestration-heavy screens and overloaded contexts

When touching `frontend-admin/**`:
- respect generated/manual/legacy API boundaries
- preserve existing auth and route protection patterns
- avoid importing React Native / Expo patterns into admin web

## Do not
- Do not introduce a parallel architecture or broad rewrite.
- Do not casually change API contracts, auth behavior, role names, or payment flows.
- Do not mix unrelated refactors into feature work.
- Do not rename or reshape public APIs, DTOs, config keys, or role semantics without checking downstream consumers.
- Do not weaken validation, auditability, authorization, or fiscal guarantees.
- Do not commit secrets.

## High-risk flows
Treat these as high-risk and change them only with explicit scope and careful validation:

- Cart → Payment → Receipt → DailyClosing
- pricing and modifier behavior
- table cart switching and recovery
- inventory / payment / order synchronization
- TSE / RKSV signing and auditability
- auth / RBAC behavior

## Repository guidance
### Backend
- Keep controllers thin.
- Prefer service or use-case extraction over controller bloat.
- Preserve current response and error-shape conventions.
- Treat migrations, money logic, receipt lifecycle, daily closing, and fiscal integrations as sensitive.

### Frontend POS
- Avoid growing orchestration screens.
- Avoid overloaded contexts.
- Keep user flows clear, stable, and resilient.
- Preserve contract compatibility with backend POS endpoints.

### Frontend Admin
- Respect generated, manual, and legacy API boundaries.
- Reuse established query, auth, and route protection patterns.
- Prefer compact, clear operator-facing feedback over noisy UI warnings.

## Validation expectations
Choose the smallest safe validation set for the touched area, and expand it when risk increases.

### Root-level validation commands
Run from repository root when relevant:

```bash
node scripts/verify-api-client.mjs
node scripts/validate-critical-openapi-paths.mjs
node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error
node localization/scripts/check-translation-boundary.mjs --app frontend-admin
node localization/scripts/check-localization-usage.mjs --app frontend-admin --strictMissing true --budgetFile localization/i18n-ci-budgets.json