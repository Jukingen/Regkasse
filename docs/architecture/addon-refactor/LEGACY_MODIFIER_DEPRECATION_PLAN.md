# Legacy Modifier – Execution-Oriented Deprecation Plan

**Date:** 2025-03-07  
**Rule:** Add-on = Product is the active architecture. Legacy modifiers = compatibility layer + migration source only. POS fallback to `group.modifiers` is **temporary transition logic**, not stable long-term.

---

## 1. Dependency Classification Table

| Item | Bucket | Why It Exists | Part of Target Architecture? | Removal Condition |
|------|--------|---------------|------------------------------|-------------------|
| **product_modifiers table** | B → C | Migration source; API populates group.modifiers | **No** | After Phase B: all active modifiers migrated; table kept for historical reference until Phase E |
| **group.modifiers in API** | B | Admin migration UI; POS fallback for groups with no products | **No** | After Phase C: POS fallback removed; after Phase D: API stops returning |
| **POS fallback to group.modifiers** | B | Groups with products empty but modifiers non-empty still need add-on display | **No** | After Phase B: 0 such groups; then remove fallback in Phase C |
| **Admin legacy section** | A → C | Migration action ("Als Produkt migrieren"); visibility for operators | **No** | After Phase B: can collapse; after Phase E: can remove section |
| **CartItemModifier table** | D | Historical carts with embedded modifiers | **No** | Phase E only: after retention period; requires data migration |
| **TableOrderItemModifier table** | D | Historical table orders with embedded modifiers | **No** | Phase E only: after retention period; requires data migration |
| **PaymentItem.Modifiers (JSON)** | D | Historical payment snapshots; receipt rendering | **No** | Phase E only: after retention period; deserialization must stay for old receipts |

**Bucket definitions:**
- **A. Required now** – Cannot operate without it.
- **B. Temporary transition only** – Needed only until migration complete; must be removed, not kept as permanent.
- **C. Removable after migration complete** – Can be reduced/removed once Phase B exit criteria met.
- **D. Historical retention only** – Read-only; no new writes; keep until retention/audit requirements satisfied.

---

## 2. Target-State Architecture Statement

### POS Add-on Source of Truth

- **`group.products`** is the **only** target-state source of truth for add-ons in POS.
- **`group.modifiers`** fallback is **transitional only**. It exists solely to support groups that have not yet been migrated.
- **Before fallback can be removed:**
  1. All active modifier groups must have at least one product in `group.products` (or have no add-ons).
  2. Zero groups in production where `products.length === 0 && modifiers.length > 0`.
  3. Migration-complete criteria (Section 3) satisfied.
- **After fallback removal:** POS must not reference `group.modifiers` in any production code path. Groups with only legacy modifiers (if any remain) must not be assigned to products, or must be treated as "no add-ons" until migrated.

---

## 3. Migration-Complete Criteria

Measurable conditions that must all be true before Phase C (remove POS fallback) and Phase D (reduce API):

| Criterion | Measurement | Target |
|-----------|-------------|--------|
| **No groups with products empty but modifiers non-empty** | `SELECT COUNT(*) FROM product_modifier_groups g WHERE g.is_active AND NOT EXISTS (SELECT 1 FROM addon_group_products a WHERE a.modifier_group_id = g.id) AND EXISTS (SELECT 1 FROM product_modifiers m WHERE m.modifier_group_id = g.id AND m.is_active)` | 0 |
| **No unmigrated active legacy modifiers** | `SELECT COUNT(*) FROM product_modifiers WHERE is_active = true` | 0 |
| **POS fallback never hit in production** | Log/metric: `Phase2.LegacyModifier.ModifierGroupDtoReturnedWithModifiers` or equivalent; no requests where POS rendered modifiers fallback | 0 hits over 7-day window |
| **Admin section collapsible** | All groups have products; "Als Produkt migrieren" has no unmigrated targets | Manual verification |
| **API can stop returning group.modifiers** | Same as above; no client (POS, admin) depends on modifiers for add-on display | After Phase C complete |

---

## 4. Phased Execution Plan

### Phase A — Stabilize Migration

**Goal:** Migration endpoint is production-safe; operators can run migration reliably.

| Aspect | Details |
|--------|---------|
| **Code changes** | ✅ Done: Product.Description, Cost set; transactional MigrateSingleByModifierIdAsync; pre-validate groupId |
| **Admin UX** | No change; migration modal already exists |
| **Backend/API** | No change |
| **POS impact** | None |
| **Risk** | Low |
| **Rollback** | Revert migration service changes; redeploy |

**Exit criteria:** Migration endpoint returns 2xx for valid requests; no HTTP 500 on product insert.

---

### Phase B — Migrate All Active Legacy Modifiers

**Goal:** Every active modifier migrated to Product; zero groups with products empty but modifiers non-empty.

| Aspect | Details |
|--------|---------|
| **Code changes** | None required; use existing migration endpoint. Optional: batch migration script/CLI for bulk run |
| **Admin UX** | Operators run "Als Produkt migrieren" for each unmigrated modifier; or run batch migration |
| **Backend/API** | No change |
| **POS impact** | None during migration; after completion, all groups have products, fallback never hit |
| **Risk** | Medium – operator discipline; ensure category selected correctly |
| **Rollback** | Migration is additive (Product + AddOnGroupProduct); modifier marked inactive. No automatic rollback; manual if needed |

**Exit criteria:**
- `SELECT COUNT(*) FROM product_modifiers WHERE is_active = true` = 0
- `SELECT COUNT(*)` from query in Section 3 (groups with products empty, modifiers non-empty) = 0

---

### Phase C — Remove POS Fallback

**Goal:** POS uses only `group.products`; no code path reads `group.modifiers` for add-on display.

| Aspect | Details |
|--------|---------|
| **Code changes** | Remove `groupsWithModifiersOnly` / `groupsWithModifiersOnly` logic from ProductRow, ProductGridCard; remove modifiers branch from ModifierSelectionBottomSheet, ModifierSelectionModal; remove `onApply`/legacy modifier handlers if no longer used; update useProductModifierGroups to filter only `group.products` |
| **Admin UX** | No change |
| **Backend/API** | API can still return group.modifiers (Phase D); no breaking change yet |
| **POS impact** | Groups with only modifiers (if any) show no add-ons; must not exist if Phase B done correctly |
| **Risk** | Low if Phase B exit criteria met; High if any group still has only modifiers |
| **Rollback** | Revert POS changes; redeploy |

**Exit criteria:** POS codebase has zero references to `group.modifiers` for add-on rendering; tests updated; 7-day production verification that no fallback path is hit.

---

### Phase D — Reduce API Payload

**Goal:** Backend stops including `g.Modifiers` in modifier group responses; smaller payload.

| Aspect | Details |
|--------|---------|
| **Code changes** | ModifierGroupsController: remove `Include(g => g.Modifiers)`; MapToModifierGroupDto: set `Modifiers = new List<ModifierDto>()` or omit. ProductController GetCatalog, GetProductModifierGroups: same. Add feature flag or query param if gradual rollout desired |
| **Admin UX** | Admin modifier-groups page: legacy section shows "Keine Legacy-Modifier" for all groups; "Als Produkt migrieren" has no targets. Section can be collapsed by default or hidden |
| **Backend/API** | Breaking: clients expecting group.modifiers get empty array. POS and admin must not depend on it (Phase C done) |
| **POS impact** | None if Phase C complete |
| **Risk** | Low |
| **Rollback** | Revert backend changes; restore Include(Modifiers) |

**Exit criteria:** API responses omit or empty group.modifiers; no client errors; observability logs confirm no legacy modifier requests.

---

### Phase E — Final Historical Cleanup

**Goal:** Optional; only after retention period and audit requirements satisfied.

| Aspect | Details |
|--------|---------|
| **Code changes** | Consider: drop product_modifiers table (only if no FK from CartItemModifier/TableOrderItemModifier; they store ModifierId but may not have FK). Do NOT drop CartItemModifier, TableOrderItemModifier, or remove PaymentItem.Modifiers deserialization without explicit data migration and legal/audit sign-off |
| **Admin UX** | Remove "Legacy-Modifier (Kompatibilität)" section entirely |
| **Backend/API** | Remove ProductModifier entity, ModifierMigrationService (or reduce to no-op) |
| **POS impact** | None |
| **Risk** | High for CartItemModifier/TableOrderItemModifier/PaymentItem.Modifiers – historical data loss |
| **Rollback** | Requires DB restore; avoid unless proven safe |

**Exit criteria:** Legal/audit confirmation; retention period passed; no carts/orders/payments with modifiers in production; or data migrated.

---

## 5. Immediate Next Actions (After Migration Bug Fix)

### Implement Now

| Action | Owner | Notes |
|--------|-------|-------|
| **Verify migration endpoint** | QA/Dev | Run single migration in staging; confirm product created with Description, no HTTP 500 |
| **Add migration-complete query** | Dev | SQL or admin report: count groups with products empty + modifiers non-empty; count active modifiers |
| **Document Phase B runbook** | Ops | Steps for operators to migrate all modifiers (single + batch) |
| **Add observability** | Dev | Log when POS receives group with modifiers but no products (fallback path); metric for "modifiers fallback hit" |

### Postpone

| Action | Until |
|--------|-------|
| Remove POS fallback (Phase C) | Phase B exit criteria met |
| Stop returning group.modifiers (Phase D) | Phase C complete |
| Drop product_modifiers (Phase E) | Retention/audit sign-off |
| Collapse admin legacy section | Phase B complete (optional earlier) |

### Monitor

| Metric | Purpose |
|--------|---------|
| `Phase2.LegacyModifier.ModifierGroupDtoReturnedWithModifiers` log count | When it reaches 0, no API responses include legacy modifiers |
| Migration endpoint success/failure rate | Ensure production-safe |
| Count of active ProductModifiers | Track migration progress |
| Count of groups with products empty, modifiers non-empty | Block Phase C until 0 |

---

## 6. Critical Gaps in Previous Recommendation

| Issue | Correction |
|-------|------------|
| **"Keep POS fallback"** sounded permanent | Explicitly classified as B (temporary transition); removal in Phase C with exit criteria |
| **"Optional: collapse legacy section"** was vague | Collapse allowed after Phase B; removal in Phase E |
| **No measurable migration-complete criteria** | Added Section 3 with concrete SQL and targets |
| **Phase ordering unclear** | Strict sequence: A → B → C → D → E; each phase has exit criteria |
| **Rollback strategies missing** | Added per-phase rollback |
| **Immediate next actions not specified** | Section 5: verify, add query, runbook, observability |

---

## 7. File Reference for Phase C Code Changes

| File | Change |
|------|--------|
| `frontend/components/ProductRow.tsx` | Remove `groupsWithModifiersOnly`, `allModifiers`, `hasModifiers`, modifier chips block |
| `frontend/components/ProductGridCard.tsx` | Same |
| `frontend/components/ModifierSelectionBottomSheet.tsx` | Remove `(group.modifiers ?? []).map` branch; products only |
| `frontend/components/ModifierSelectionModal.tsx` | Same |
| `frontend/hooks/useProductModifierGroups.ts` | Filter: `(g) => (g.products?.length ?? 0) > 0` only (no modifiers fallback) |
| `frontend/__tests__/addOnFlow.test.ts` | Remove `groupsWithModifiersOnly` tests; update for products-only |

---

## 8. Summary

- **product_modifiers**: B (transition) → C (removable after migration) → E (table drop optional)
- **group.modifiers in API**: B → D (stop returning)
- **POS fallback**: B → C (remove)
- **Admin legacy section**: A (now) → C (collapse after B) → E (remove)
- **CartItemModifier, TableOrderItemModifier, PaymentItem.Modifiers**: D (historical retention); Phase E only, high risk

**Next:** Complete Phase A verification; run Phase B migration; then execute Phase C with measured exit criteria.
