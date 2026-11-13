# Agent task template

## 0) Analyze first (required)

- Read related existing code and tests; do not assume.
- Short finding: what the current behavior is, what will change, why it is safe.

## 1) Goal

- Requested change:
- Out of scope:

## 2) Impact area

- Backend: Y/N
- Frontend POS: Y/N
- Frontend Admin: Y/N
- DB/Migration: Y/N
- OpenAPI/Generated client: Y/N
- Multi-tenant (host/slug, `tenant_id`, query filter, Super Admin): Y/N
- Singleton + EF (`IServiceScopeFactory`, root `IDbContextFactory` forbidden): Y/N

## 3) Risk note

- Compliance/fiscal impact?
- Auth/RBAC impact?
- Tenant isolation / cross-tenant access impact?
- Backward-compatibility risk?

## 4) Plan (short)

- Step 1
- Step 2
- Step 3

## 5) Verification

- **Targeted tests** (fiscal area: relevant `KasseAPI_Final.Tests` filters or contract scripts)
- Other scripts/commands to run (`verify-api-client`, OpenAPI critical path, i18n, and similar)
- Expected result

## 6) Output format

- **Affected files** (full path or a clear repo-relative list)
- Short rationale
- Test/script results
- **Risk summary** (fiscal, auth, backward compatibility)
- Remaining unknowns

## 7) Final audit

- Is the behavior change limited to what was requested?
- If Swagger + Orval were affected, are they in sync?
- No log/PII/voucher leak in sensitive areas?
- Consistency check against `REGKASSE_AI_ONBOARDING.md` and related `/ai` items if needed
