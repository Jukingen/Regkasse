# Frontend Admin — Testing strategy

**Owner:** FA maintainers  
**Last updated:** 2026-07-21  

## Goals

| Layer | Tool | Purpose |
| ----- | ---- | ------- |
| Unit / component | Vitest + Testing Library | Pure utils, hooks, small UI (modals, filters) |
| Coverage gate | `npm run test:coverage` | ≥ **80% lines** on logic helpers (see scope below) |
| E2E | Playwright (`npm run test:e2e`) | Critical flows with mocked API |
| Contract / truth | Focused Vitest suites | RKSV / operator-copy / Orval smoke |

## Coverage gate (enforced)

`vitest.config.ts` → `coverage.include` / `exclude` / `thresholds`.

**In scope (must stay ≥80% lines):**

- `src/features/**/utils/**`
- `src/features/**/logic/**` (minus deferred presentation mappers — see exclude list)
- `src/shared/utils/**`, selected `shared/auth/*` helpers
- `src/lib/logging/**` (except `serverLogger.ts`)
- `src/lib/monitoring/**` (except Sentry I/O facades)
- `src/lib/validations/**`, `dateFormatter`, `httpCancellation`
- Selected hooks: `useDebounce`, `usePermissions`, `useCanAccessPath`
- `src/i18n/formatting.ts`

**Thresholds:** lines **80%**, statements **75%**, functions **70%**, branches **60%**.

**Out of gate (intentionally):** App Router pages, large Ant Design feature screens, Orval `generated/`, Backup DR presentation mappers, Sentry SDK wrappers. Those use component/E2E tests instead of line-coverage chasing.

Run:

```bash
cd frontend-admin
npm run test:coverage
```

Open HTML report: `coverage/index.html`.

> Always run the **full** Vitest suite for coverage. A path-filtered run under-counts because many utils are exercised only from feature tests elsewhere.

### Baseline (2026-07-21)

| Scope | Lines (approx.) |
| ----- | --------------- |
| Broad FA include (pre-gate, historical) | ~24% |
| **Current coverage gate** | **~82% lines** (after utils/hooks additions) |

## What to test

### Utilities (`**/utils`, `**/logic`)

- Happy path + **edge cases** (null/empty, invalid dates, UUID paths)
- **Error / boundary** branches (missing deadlines, empty filters)
- Prefer pure functions — no network

### Hooks

- `renderHook` + fake timers (`useDebounce`)
- Auth/permission hooks with mocked providers
- Abort / cancel paths where applicable (`httpCancellation`)

### Components (RTL)

- User interactions: click confirm/cancel, disabled-while-loading (`ConfirmDialog`)
- Prefer accessible queries (`getByRole`)
- Mock `next/navigation` and heavy data hooks at the boundary

### E2E

- Login smoke, key Super Admin / Mandanten routes
- Mocked API by default (`frontend-admin-e2e` / `frontend-admin-ci`)

## Adding tests (checklist)

1. Colocate `__tests__/` next to the module (or under `src/lib/__tests__`).
2. Cover at least one edge + one error/empty case for utils.
3. Re-run `npm run test:coverage` and keep the gate green.
4. Do **not** remove deferred files from `coverage.exclude` without adding unit coverage.

## Known gaps / next campaigns

| Area | Notes |
| ---- | ----- |
| `features/backup-dr/logic/**` | Large presentation surfaces — component integration tests exist; unit campaign deferred |
| Filter URL serializers | `productFilterUrl` / `paymentFilterUrl` deferred |
| Sentry facades | Exercised via production wiring; keep out of unit gate |
| Failing legacy suites | Some Backup DR / TenantsTable / ReportFilters suites fail under full run — fix separately from coverage gate |

Track residual debt in `TECHNICAL_DEBT.md` when opening a coverage campaign.

## Related

- [README.md](../README.md) — Testing section
- [MONITORING.md](./MONITORING.md) — runtime monitoring (not unit coverage)
- Playwright: `tests/e2e/`, `playwright.config.ts`
