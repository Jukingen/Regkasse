# Phase 2 – Production Rollout and Release Safety Plan

Safe rollout plan for the add-on refactor (legacy modifiers → sellable add-on products). No automatic behavior change; legacy read/write remains until Phase 3.

---

## 1. Recommended deployment order

| Phase | Step | What | When |
|-------|------|------|------|
| **A** | A1 | Apply DB migrations (schema only) to production. | Before or with backend deploy. |
| **A** | A2 | Deploy Phase 2 backend (creation frozen, flat paths active, legacy compat kept). | First deploy. |
| **B** | B1 | Deploy Admin and POS that prefer add-on products and flat cart. | After backend is live and stable. |
| **C** | C1 | Run legacy-modifier migration (dry-run in staging first, then production when ready). | After B; can be same day or later. |
| **D** | D1 | Post-release validation (cart, payment, receipt, history). | Within 24–48 h of C1. |

**Rule:** Backend before frontend. Migration after clients can use products. No big-bang; each step is independently safe.

---

## 2. Required DB migration order

Apply EF Core migrations in chronological order. Phase 2–relevant migrations:

| Order | Migration name | Purpose |
|-------|----------------|---------|
| 1 | (all prior migrations) | Existing schema. |
| 2 | `AddProductIsSellableAddOnAndAddOnGroupProducts` | `products.is_sellable_addon`, `addon_group_products` table. |
| 3 | `AddProductLegacyModifierIdForMigration` | `products.legacy_modifier_id` (nullable FK to `product_modifiers.id`) for idempotent migration. |

**Commands (run from backend directory):**

```bash
cd backend
dotnet ef database update
```

**Production practice:**

- Prefer running `dotnet ef database update` from a release pipeline or maintenance window rather than relying only on in-process `Migrate()` at startup.
- **Startup gate:** The application already throws on startup if pending migrations exist (Program.cs). Ensure migrations are applied before or during deploy so the app can start.

**Rollback (schema):** To remove the Phase 2 migration column only, revert to the migration before `AddProductLegacyModifierIdForMigration` (not recommended until Phase 3; see rollback section).

---

## 3. Staging verification checklist

Before production:

- [ ] **Schema:** All migrations applied on staging DB; no pending migrations; app starts.
- [ ] **Backend:** Phase 2 backend deployed; POST …/modifiers returns **410 Gone**.
- [ ] **Admin:** Add-on groups show “+ Produkt” (no “+ Modifier (Legacy)”); can add products to groups; group list/detail returns both `products` and `modifiers` (legacy read).
- [ ] **POS – new flow:** Add base product; add add-on via group’s product (not legacy modifier); cart shows two lines; payment succeeds; receipt shows two flat lines.
- [ ] **POS – legacy flow:** Cart with existing legacy modifiers (or create via old payload) loads; payment with `ModifierIds` succeeds; receipt shows nested “+ Extra” where applicable.
- [ ] **Migration dry-run:** `POST /api/admin/migrate-legacy-modifiers` with `dryRun: true` (or CLI `--dryrun`) returns report; no new rows in `products` or `addon_group_products`.
- [ ] **Migration real run:** Run migration with valid `defaultCategoryId`; verify created products and `addon_group_products`; run again and confirm same modifiers in `Skipped` (idempotent).
- [ ] **Table orders:** Create table order from cart (with legacy modifiers if possible); call table-orders-recovery; response includes `SelectedModifiers` where applicable.
- [ ] **History:** Open an old receipt/payment that had modifiers; totals and lines render correctly.

---

## 4. Backward compatibility checkpoints

| Checkpoint | What to verify |
|------------|----------------|
| **API – cart** | Old clients sending `selectedModifiers` on add-item/update-item still get 200; GetCart returns `selectedModifiers` for legacy lines. |
| **API – payment** | Requests with `modifierIds` / `modifiers` on a product line still accepted; payment created; totals include modifier amounts. |
| **API – modifier groups** | GET group returns both `products` and `modifiers`; no removal of `modifiers`. |
| **Data** | No deletion of `product_modifiers`, `cart_item_modifiers`, `table_order_item_modifiers`; PaymentDetails.PaymentItems JSON still supports `Modifiers` array. |
| **Receipt** | Payments that have `PaymentItem.Modifiers` in JSON still produce correct receipt (main line + nested modifier lines). |

If any checkpoint fails, treat as a rollback trigger (see below).

---

## 5. Rollback strategy

### 5.1 Migration problems (duplicate products, wrong category, partial run)

- **Prevention:** Always run dry-run first; use a dedicated add-on category; run migration from one process only (no parallel CLI/API).
- **Rollback:** Migration does **not** delete legacy data. To “undo” migrated products:
  - Manually delete the created products (and their `addon_group_products` rows) if needed.
  - Optionally set `products.legacy_modifier_id = NULL` for those products if you want to re-run migration later for them.
- **Schema:** Reverting the `AddProductLegacyModifierIdForMigration` migration is possible but only if no application code relies on the column; prefer application rollback (previous backend version) and manual cleanup of migrated products if necessary.

### 5.2 Payment issues (totals wrong, TSE/receipt failures)

- **Prevention:** Phase 2 does not change tax/totals logic for existing flows; add-on products use same CartMoneyHelper path. Test flat and legacy payment in staging.
- **Rollback:** Deploy previous backend version. Ensure DB migrations already applied are backward-compatible (Phase 2 migrations are additive only).
- **Data:** No payment data need be reverted; old backend will still read existing PaymentDetails and PaymentItems JSON (including `Modifiers`).

### 5.3 Receipt issues (wrong lines, double “+”, missing modifier lines)

- **Prevention:** Staging tests for flat and legacy receipt; frontend only displays what backend sends (no double “+”).
- **Rollback:** Deploy previous backend/frontend. Receipt content is derived from stored PaymentDetails; no receipt-specific rollback of data.

### 5.4 POS / client mismatch (old POS with new backend or vice versa)

- **Old POS + new backend:** Supported. Backend still accepts `selectedModifiers` and `modifierIds`; legacy paths remain.
- **New POS + old backend:** If new POS sends only product lines (no modifierIds) and old backend expects modifiers for some flows, payment could fail. **Mitigation:** Deploy backend first, then POS.
- **Rollback:** Revert frontend to previous version; backend remains compatible with old clients.

---

## 6. Temporary feature flags or guards

Current implementation does not use feature flags for Phase 2 behavior:

- **Legacy creation:** Already “off” by design (410 at POST …/modifiers). No flag needed.
- **Flat vs legacy paths:** Chosen by data (e.g. `IsSellableAddOn`, presence of `ModifierIds`). No runtime flag.

**Optional future improvement (not required for Step 10):** A config or feature flag could gate the **migration API** (e.g. only allow `POST /api/admin/migrate-legacy-modifiers` when a flag is true) so production migration is only possible after explicit enablement. Today, migration is already operator-controlled (admin-only + explicit call); a flag would add an extra safety switch if desired.

**Existing guard:** The startup check for pending EF migrations prevents running with schema drift; keep it.

---

## 7. Operator checklist: running legacy modifier migration safely

Use this when running the modifier → product migration in staging or production.

**Prerequisites**

- [ ] Add-on category exists (e.g. “Zusatzprodukte”); note its GUID.
- [ ] DB migrations applied (`dotnet ef database update` or equivalent).
- [ ] No other process will run the migration at the same time.

**Staging**

1. [ ] Call migration with **dryRun: true** (API or CLI `--dryrun`).
2. [ ] Check response: `totalProcessed`, `migratedCount`, `skippedCount`, `errorCount`; inspect `errors` if any.
3. [ ] Run migration with **dryRun: false**.
4. [ ] Verify in Admin: modifier groups show new products; legacy modifiers still visible.
5. [ ] Run migration again; confirm previously migrated modifiers appear in `skipped` (idempotent).

**Production**

1. [ ] Schedule a short maintenance window or low-traffic period (optional but recommended).
2. [ ] Dry-run in production (optional): `dryRun: true` to see current counts.
3. [ ] Run migration (API or CLI) with valid `defaultCategoryId`.
4. [ ] Log response (migrated/skipped/errors) for audit.
5. [ ] If `errorCount > 0`: review `errors`; fix data or config (e.g. inactive group, missing category); re-run if needed (idempotent).
6. [ ] Spot-check Admin: one or two groups show new products; receipts/payments still load.

**CLI example**

```bash
cd backend
dotnet run -- migrate-legacy-modifiers "<CategoryGuid>" --dryrun   # dry run
dotnet run -- migrate-legacy-modifiers "<CategoryGuid>"            # real run
```

**API example (PowerShell)**

```powershell
$token = "<AdminJWT>"
$body = '{"defaultCategoryId":"<CategoryGuid>","dryRun":false}' 
Invoke-RestMethod -Method Post -Uri "https://<host>/api/admin/migrate-legacy-modifiers" -Headers @{ Authorization = "Bearer $token" } -ContentType "application/json" -Body $body
```

---

## 8. Post-release validation checklist

Within 24–48 hours of production migration:

- [ ] **Cart:** New order with add-on product only (no legacy modifiers): two lines; no crash; totals correct.
- [ ] **Cart:** Legacy cart (if any still open) loads; `selectedModifiers` present where applicable.
- [ ] **Payment:** Complete payment with mixed (base + add-on product) items; success; totals and tax match.
- [ ] **Receipt:** New flat receipt: one line per product; no double “+”.
- [ ] **Receipt:** Old receipt (with embedded modifiers) still displays correctly.
- [ ] **Admin:** Modifier groups list and detail; new products visible; no errors in console/network.
- [ ] **Table orders:** Recovery endpoint returns table orders; items with legacy modifiers show `SelectedModifiers`.
- [ ] **Logs:** No spike in 4xx/5xx on cart, payment, or receipt endpoints; no critical/exceptions related to modifiers or migration.

---

## 9. Rollout phases (summary)

| Phase | Scope | Owner | Exit criterion |
|-------|--------|--------|----------------|
| **Pre-release** | DB migrations applied; backend + frontend deployed to staging; staging checklist passed. | DevOps / QA | All staging checks green. |
| **Release – backend** | Deploy Phase 2 backend to production; apply migrations if not auto. | DevOps | App healthy; 410 on POST …/modifiers. |
| **Release – frontend** | Deploy Admin + POS. | DevOps | Admin and POS use new flows where applicable; legacy still works. |
| **Release – migration** | Run legacy-modifier migration (dry-run then real) in production. | Ops / Admin | Migration report logged; groups show new products. |
| **Post-release** | Execute post-release validation checklist. | QA / Ops | No regressions; logs clean. |

---

## 10. Pre-release checklist

- [ ] All Phase 2 tests pass (`dotnet test`).
- [ ] Staging DB has migrations applied; app starts.
- [ ] Staging verification checklist (section 3) completed.
- [ ] Add-on category created in staging and production (or creation process documented).
- [ ] Rollback plan and previous app/DB version documented.
- [ ] Operators know how to run migration (CLI and/or API) and where to find logs.

---

## 11. Release-day checklist

- [ ] Backup production DB (or confirm automated backup) before migration.
- [ ] Apply DB migrations (if not auto-applied during deploy).
- [ ] Deploy backend; confirm startup and no pending-migration exception.
- [ ] Deploy Admin and POS.
- [ ] Smoke: login, load cart, load one modifier group (products + modifiers).
- [ ] Run migration (dry-run then real) per operator checklist; save migration report.
- [ ] Spot-check: one group in Admin shows new products; one new sale with add-on product; one old receipt still correct.
- [ ] Monitor logs and errors for 1–2 hours.

---

## 12. Rollback checklist

If a rollback is decided:

- [ ] **Application:** Deploy previous backend/frontend version. No DB rollback needed for Phase 2 (additive schema).
- [ ] **Migration “undo”:** If migration created unwanted products, delete those products and their `addon_group_products` rows (and optionally clear `legacy_modifier_id`) manually; document for re-migration later if needed.
- [ ] **Schema rollback:** Only if necessary and no code depends on new columns: revert EF migration (e.g. `dotnet ef database update <PreviousMigrationName>`). Prefer application rollback over schema revert.
- [ ] **Communication:** Inform support and ops; check monitoring and logs after rollback.
- [ ] **Post-rollback:** Re-run smoke tests; schedule root-cause and retry.

---

## 13. Risks and mitigations

| Risk | Mitigation |
|------|------------|
| **Pending migrations at startup** | App throws and does not start; apply migrations in pipeline or before deploy. |
| **Migration run twice in parallel** | Document single-run policy; use CLI or API from one place; idempotency limits duplicate products. |
| **Wrong or missing category** | Migration returns error; no partial writes; create category and re-run. |
| **Old POS sends only legacy modifiers** | Backend still accepts and processes; no change until Phase 3. |
| **New POS with old backend** | Deploy backend first; then POS. |
| **TSE/fiscal or tax regression** | Phase 2 does not change tax logic; same CartMoneyHelper; test in staging. |
| **Receipt layout or totals wrong** | Staging tests for flat and legacy; rollback to previous frontend/backend if needed. |
| **Table order recovery broken** | Legacy TableOrderItemModifiers still loaded and serialized; covered by Phase 2 tests. |
| **Admin cannot add products to groups** | Verify addon_group_products and group.products in staging; rollback backend if broken. |

---

## 14. References

- [PHASE2_IMPLEMENTATION.md](./PHASE2_IMPLEMENTATION.md) – Overview and rollout strategy.
- [LEGACY_MODIFIER_MIGRATION.md](./LEGACY_MODIFIER_MIGRATION.md) – How to run migration and response shape.
- [CART_PAYMENT_RECEIPT_SIMPLIFICATION.md](./CART_PAYMENT_RECEIPT_SIMPLIFICATION.md) – Flat vs legacy behavior.
- `ai/PHASE2_TEST_COVERAGE.md` – Test coverage for Phase 2.
- `ai/PHASE2_LEGACY_MODIFIER_CLEANUP_ANALYSIS.md` – Remaining legacy surfaces and Phase 3 order.
