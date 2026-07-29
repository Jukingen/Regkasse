# Frontend-Admin improvement validation checklist

**Audience:** Maintainers / QA verifying the 2026 FA cleanup batch.  
**When to use:** After **all** related improvements below are implemented and merged locally (or on the PR branch). Treat this file as a go / no-go gate before shipping.

**Related improvements in scope**

| Area | Intent |
|------|--------|
| Manual `api/admin/*` clients | Shared `httpHelpers` (`SecondParameter` / `unwrapData`) — no local `any` helpers |
| `useNotify` P1 (RKSV) | Replace `useAntdApp().message` toasts; i18n keys for hardcoded DE; `notify.apiError` for API failures; keep `modal` on `useAntdApp` |
| `useNotify` P2 (License & Billing) | Same toast rules on license hooks/components + billing sales/new sale |
| Remaining `any` cleanup | `useURLFilters`, Category/Customer lists, `DebounceSelect`, `productMapper`, `ProductForm` |
| Recharts lazy-load | Extract chart components; `next/dynamic` (`ssr: false` + Skeleton) on dashboard widgets + TSE analytics |

---

## Next step (read this first)

1. **Implementation on this branch is complete** for the scope table below (`httpHelpers`, P1/P2 `useNotify`, `any` cleanup, recharts lazy-load).
2. **Run this checklist** top to bottom and tick every box (§0 → §3).
3. Only mark the release **Go** when §4 is fully satisfied.

If you re-open this doc on an older branch, use the status table at the bottom and skip rows for packages that are still pending.

---

## 0. Prerequisites

- [ ] Backend + FA running (`npm run dev:backend`, `npm run dev:admin` from repo root / package scripts)
- [ ] Dev tenant selected
- [ ] Exercise both **Super Admin** and **Mandanten-Admin** where permissions differ
- [ ] UI locale: at least **de**, once **en** (catch missing i18n keys)

---

## 1. Manual tests — `useNotify` migration

For each step: toast appears, theme-aware, translated (locale switch changes copy), API failures use `apiError` / fallback key (not a raw `response.data.message` dump). Confirm dialogs still use `useAntdApp().modal` (not static `Modal.confirm`).

### 1A — P1 RKSV

| # | Page / action | Expected | Done |
|---|----------------|----------|------|
| 1 | RKSV Sonderbelege — Null/Start/Jahres **without** register | Warning: select register | [ ] |
| 2 | Create **Nullbeleg** | Success i18n key; list refreshes | [ ] |
| 3 | Create **Startbeleg** (eligible register) | Success | [ ] |
| 4 | **Jahresbeleg** — confirm modal → OK / Cancel | Modal unchanged; success toast on OK; no toast on Cancel | [ ] |
| 5 | **Schlussbeleg** — wrong confirm text / correct `ENDBELEG` | Validation error / success | [ ] |
| 6 | Past-month **Monatsbeleg** (`CreateMonatsbelegModal` / `LateMonatsbelegCreationCard`) | Success or warning confirm; failure → `apiError` | [ ] |
| 7 | `MonatsbelegTimeline` — copy link | `successKey` / `errorKey` | [ ] |
| 8 | Dashboard RKSV reminder — create Monatsbeleg (no permission / already exists) | warning / info / error keys | [ ] |
| 9 | DEP export push settings — save | saved / saveFailed keys | [ ] |
| 10 | Single signature verify — empty / invalid | warning / `apiError` | [ ] |
| 11 | DEP export test — export / schedule | success keys; fail → `apiError` or key | [ ] |

### 1B — P2 License & Billing

| # | Page / action | Expected | Done |
|---|----------------|----------|------|
| 12 | Super Admin tenant license overview — CSV (empty / with rows) | info / success (`exported` + count) | [ ] |
| 13 | License edit modal — save | `successKey`; API fail → `apiError` | [ ] |
| 14 | License extend — invalid / expired / wrong-tenant key | Domain-mapped messages (`previewError*`), not generic dump | [ ] |
| 15 | License test panel (Dev) — scenario / manual date | success from helpers; no tenant → warning | [ ] |
| 16 | License usage analytics — CSV export | exportSuccess / exportFailed | [ ] |
| 17 | Billing sales — cancel / PDF download | cancelSuccess; errors → `apiError` | [ ] |
| 18 | Billing new sale — preview / create / PDF preview | successKeys (+ invoiceNumber); `apiError` (PDF fallback key preserved) | [ ] |

### 1C — Smoke (non-toast regression)

| # | Area | Check | Done |
|---|------|--------|------|
| 19 | Products / Categories list | Loads; create/edit still works | [ ] |
| 20 | Vouchers / benefits / pricing / payment methods | CRUD smoke (`httpHelpers` consumers) | [ ] |

---

## 2. Code checks — `api/admin` typing + typecheck

Run from `frontend-admin/`:

```bash
npm run typecheck
```

- [ ] Exit code 0, **or** only known unrelated errors (document them); **no** new errors under `src/api/admin/`
- [ ] Typecheck output does **not** mention: `products.ts`, `categories.ts`, `pricing-rules.ts`, `benefit-definitions.ts`, `benefit-assignments.ts`, `payment-method-definitions.ts`, `vouchers.ts`

Local helpers must be gone; shared import present:

```bash
# Expect: no local helpers in client files (httpHelpers.ts only if matched)
rg "function unwrapData|type SecondParameter" src/api/admin --glob "*.ts"

# Expect: products + six siblings
rg "from '@/api/admin/httpHelpers'" src/api/admin
```

- [ ] First command: no local `unwrapData` / `SecondParameter` in the seven clients
- [ ] Second command: seven files import `@/api/admin/httpHelpers`

After **`any` cleanup** lands:

```bash
rg "\bany\b" src/hooks/useURLFilters.ts src/components/DebounceSelect.tsx \
  src/features/categories/components/CategoryList.tsx \
  src/features/customers/components/CustomerList.tsx \
  src/features/products/utils/productMapper.ts \
  src/features/products/components/ProductForm.tsx
```

- [ ] No `any` in those targets; `npm run typecheck` still clean for them

After **P1 hardcode → i18n** lands:

```bash
npm run i18n:validate:ci
```

- [ ] Passes (`strictMissing` + orphan policy)

Optional:

```bash
npm test -- src/api
# and/or relevant feature vitests
```

---

## 3. Performance — `recharts` lazy-load

### DevTools setup

1. Chrome → Network → enable **Disable cache**
2. Hard reload
3. Filter: `JS` (and/or search `recharts`, `Chart`, chunk names)

### Dashboard

| # | Check | Expected | Done |
|---|--------|----------|------|
| 1 | First paint with chart widgets off / not mounted | Large `recharts` not required in the critical path | [ ] |
| 2 | Show **Today Sales** / **Payment Trends** widgets | Separate async chunk (`TodaySalesChart`, `PaymentTrendCharts`, `recharts`, or hashed chunk) | [ ] |
| 3 | Waterfall | Chart chunk loads **after** widget mount (lazy) | [ ] |
| 4 | Second visit (cache on) | Chunk from cache; UI still works | [ ] |

### TSE analytics (`/admin/tse/analytics`)

| # | Check | Expected | Done |
|---|--------|----------|------|
| 5 | Page shell (KPI / tabs) | Shell JS first; charts not necessarily in same initial chunk | [ ] |
| 6 | Chart area visible | `TseAnalyticsCharts` / recharts chunk loads; Area/Pie render | [ ] |

### Negative / regression

- [ ] `LicenseDashboardBarChart` / `LicenseUsageTrendChart` still lazy (no regression)
- [ ] No `from 'recharts'` on widget shells / TSE **page** — only on extracted `*Chart*.tsx` modules

Optional bundle analysis:

```bash
npm run analyze
```

- [ ] `recharts` appears in async chunks, not glued into the main dashboard entry

---

## 4. Go / No-go

**Go** only if all apply:

- [ ] §1A + §1B toasts OK (locale + modal separation)
- [ ] §2 typecheck / rg / i18n OK for landed packages
- [ ] §3 Network shows lazy chart chunks; static `recharts` imports confined to chart modules

**No-go examples**

- Hardcoded German toast after P1, or missing i18n key (`i18n:validate:ci` fails)
- Catch still shows raw API strings; toast still via `useAntdApp().message` on migrated files
- Typecheck errors in `unwrapData` / `SecondParameter` / `ApiProduct` / ProductForm
- Dashboard initial load pulls full `recharts` into the main entry after lazy-load work

---

## Implementation status note (update as you go)

Track what is already on the branch before running the full list:

| Package | Status (edit) |
|---------|----------------|
| `api/admin` → `httpHelpers` | **done** |
| `useNotify` P1 RKSV | **done** |
| `useNotify` P2 License & Billing | **done** |
| Remaining `any` cleanup | **done** |
| Recharts lazy-load | **done** (Dashboard.tsx unchanged — registry + chart extract) |

When a package is still pending, skip its manual/network rows but keep §0 and any applicable §2 checks for what already landed.

---

**Last updated:** 2026-07-29
