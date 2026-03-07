# Phase 2 Implementation – Add-on Refactor

Technical overview of the Phase 2 refactor: moving from legacy modifiers to sellable add-on products while keeping backward compatibility.

---

## 1. Why the change was made

- **Unified model:** Add-ons are products. One catalog (products + categories), one cart/payment/receipt model (one line per product). No separate “modifier” catalog or nested modifier structures for new flows.
- **Simpler flows:** Cart = list of product lines. Payment = list of product lines. Receipt = one line per product. No special handling for “base + modifiers” in new code.
- **RKSV/fiscal alignment:** Add-ons as products keep tax and totals consistent (one product = one line; VAT from product category).
- **Maintainability:** Less branching (legacy vs new), clearer DTOs (products first, modifiers deprecated), and a single migration path from old modifier data to products.

---

## 2. Old vs new architecture

### Old (legacy)

| Layer | Behaviour |
|-------|-----------|
| **Catalog** | `ProductModifier` rows per modifier; `ProductModifierGroup`; assignment via `ProductModifierGroupAssignment` (product ↔ group). Modifiers are not products. |
| **Cart** | Base product line + embedded `CartItemModifier` rows (selected modifiers on that line). Same product + same modifier set = one line, quantity merged. |
| **Payment** | One `PaymentItem` per product line; `PaymentItem.Modifiers` (snapshots) for embedded modifiers. Totals = product + sum(modifiers). |
| **Receipt** | Main line per product; nested “+ Extra” lines from `PaymentItem.Modifiers`. |

### New (target)

| Layer | Behaviour |
|-------|-----------|
| **Catalog** | Add-ons are `Product` with `IsSellableAddOn = true`. Link to groups via `AddOnGroupProduct` (group ↔ product). Legacy `ProductModifier` still exists for read/migration only. |
| **Cart** | One `CartItem` per product (base or add-on). No `CartItemModifier` for new add-ons; same add-on product = one line, quantity merged. |
| **Payment** | One `PaymentItem` per product line; `Modifiers` empty for add-on products. No modifier snapshots for new add-on sales. |
| **Receipt** | One receipt line per product; no nesting for new add-on products. |

### Coexistence (Phase 2)

- **Read:** Legacy modifier data (cart_item_modifiers, PaymentItem.Modifiers, product_modifiers) is still loaded and returned. Historical carts, payments, and receipts keep working.
- **Write – new behaviour:** Add-on products → flat cart line, flat payment line, flat receipt line. No new `CartItemModifier` or `PaymentItem.Modifiers` for add-ons.
- **Write – legacy:** Base product + `SelectedModifiers` / `ModifierIds` still accepted; backend still writes `CartItemModifier` and `PaymentItemModifierSnapshot` for backward compatibility until Phase 3.

---

## 3. Migration strategy (high level)

1. **Freeze legacy creation**  
   POST …/modifiers returns 410 Gone. No new legacy modifier rows. Admin encourages “+ Produkt” (add-on product) instead.

2. **Data migration**  
   Run modifier → product migration (see [LEGACY_MODIFIER_MIGRATION.md](./LEGACY_MODIFIER_MIGRATION.md)). Creates `Product` + `AddOnGroupProduct` per legacy modifier; `Product.LegacyModifierId` for idempotency. Legacy rows are not deleted.

3. **Cart / payment / receipt simplification**  
   New add-ons use flat model only. Legacy paths still read and write modifier structures (see [CART_PAYMENT_RECEIPT_SIMPLIFICATION.md](./CART_PAYMENT_RECEIPT_SIMPLIFICATION.md)).

4. **DTO and API**  
   Products first in DTOs; modifier fields deprecated (`[Obsolete(..., false)]`), still present for compatibility.

5. **Phase 3 (later)**  
   Stop writing legacy modifier structures; then deprecate/remove legacy fields and tables when clients and data policy allow.

---

## 4. Compatibility approach

- **Backward compatible:** All existing API contracts and stored data remain valid. Legacy carts, orders, and payments continue to load and display.
- **Soft deprecation:** Legacy DTO properties and modifier write paths are annotated and documented; no removal in Phase 2. New clients should use products and flat cart/payment only.
- **Dual read:** Backend and frontend support both flat (product-only) and legacy (nested modifiers) payloads for receipt and history.
- **No API versioning yet:** No `/api/v2`; deprecation is by documentation and `[Obsolete]` only. Removal will follow a communicated deprecation period.

---

## 5. Rollout strategy

| Step | Action | Owner |
|------|--------|--------|
| 1 | Deploy Phase 2 backend (creation frozen, flat paths active, legacy read/write kept). | Backend |
| 2 | Deploy Admin/POS that prefer add-on products and flat cart (add-on as separate line). | Frontend |
| 3 | In staging: run legacy-modifier migration (dry-run then real run). | Ops/Admin |
| 4 | Verify: new add-ons as products, flat cart/payment/receipt; old receipts and history still correct. | QA |
| 5 | Production: run migration when ready; monitor logs and report (migrated/skipped/errors). | Ops |
| 6 | Phase 3 only after all clients use product-only flow and no (or negligible) active legacy carts. | Product/Eng |

---

## 6. Known risks

| Risk | Mitigation |
|------|------------|
| **Concurrent migration runs** | Run migration from a single process; avoid parallel CLI/API runs. Document in runbook. |
| **Wrong or missing category** | Migration validates category; single error, no partial writes. Use staging first. |
| **Old clients still send modifiers** | Backend still accepts and processes; no breaking change until Phase 3 “ignore legacy on write”. |
| **Historical data** | Legacy read paths and DB tables kept; no drop of modifier tables in Phase 2. |
| **TSE / fiscal** | Add-on products use same tax/totals logic as other products; no change to TSE/fiscal behaviour. |

---

## 7. Future Phase 3 cleanup items

- **Backend – write paths:** Ignore `SelectedModifiers` / `ModifierIds` on cart and payment; stop writing `CartItemModifier`, `PaymentItemModifierSnapshot`, `TableOrderItemModifier` for new operations.
- **DTOs:** Remove or always-empty legacy properties after deprecation period (or new API version).
- **Services:** Narrow or remove `ProductModifierValidationService` usage for cart/payment; keep for migration/read if needed.
- **Endpoints:** Remove or keep POST …/modifiers as 410 stub.
- **Data/schema:** After no read dependency and data policy allows: archive/drop `cart_item_modifiers`, `table_order_item_modifiers`; later consider `product_modifiers` and `Product.LegacyModifierId`.

See [LEGACY_MODIFIER_CLEANUP_ANALYSIS](../../../ai/PHASE2_LEGACY_MODIFIER_CLEANUP_ANALYSIS.md) for the full list and recommended removal order.

---

## 8. References

- [LEGACY_MODIFIER_MIGRATION.md](./LEGACY_MODIFIER_MIGRATION.md) – How to run and design the modifier → product migration.
- [CART_PAYMENT_RECEIPT_SIMPLIFICATION.md](./CART_PAYMENT_RECEIPT_SIMPLIFICATION.md) – Flat cart, payment, and receipt behaviour.
- `ai/PHASE2_*.md` – Step-by-step implementation notes and cleanup analysis.
- `ai/PHASE2_TEST_COVERAGE.md` – Test coverage for Phase 2.
