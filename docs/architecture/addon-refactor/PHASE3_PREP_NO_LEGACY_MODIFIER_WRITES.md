# Phase 3 Prep: No New Legacy Modifier Writes

**Goal:** Keep read compatibility for historical data, but stop creating any new legacy modifier records. DTOs are not removed; request fields are accepted and ignored for write.

---

## Summary of changes

| Area | Before | After |
|------|--------|--------|
| **Cart add-item** | Base product + `SelectedModifiers` → validated and persisted as `CartItemModifier`. | `SelectedModifiers` accepted in request but **ignored for write**. Base product stored as product-only; merge by CartId + ProductId (no modifiers). Add-ons as separate add-item calls. |
| **Cart update-item** | `SelectedModifiers` in request → replace cart item modifiers in DB. | `SelectedModifiers` accepted but **ignored for write**. Quantity/Notes still updated; existing `CartItem.Modifiers` left unchanged. |
| **Payment create** | `ModifierIds` / `Modifiers` on item → validated and stored as `PaymentItem.Modifiers`. | **Ignored for write.** All lines treated as product-only; add-ons must be separate payment items (productId). |
| **Table-order create/update** | Cart → TableOrder copied `CartItem.Modifiers` to `TableOrderItemModifier`. | **No new `TableOrderItemModifier` rows.** Totals still computed from cart (including modifier amounts when present); only persistence of modifier rows is disabled. |

---

## Read compatibility (unchanged)

- **Cart:** `GetCart` / `BuildCartResponse` still load and return `CartItem.Modifiers` as `SelectedModifiers` for existing data.
- **Payment:** Existing `PaymentItem.Modifiers` still loaded and used for receipt/totals.
- **Table order:** `GetTableOrdersForRecovery` still loads `TableOrderItem.Modifiers` and `CartItem.Modifiers`, maps to `SelectedModifiers`.
- **DTOs:** `AddItemToCartRequest.SelectedModifiers`, `UpdateCartItemRequest.SelectedModifiers`, `PaymentItemRequest.ModifierIds`/`Modifiers`, `TableOrderItemInfo.SelectedModifiers` remain; no versioning or removal.

---

## Files changed

| File | Change |
|------|--------|
| **backend/Controllers/CartController.cs** | Add-item: legacy path replaced with product-only merge (no `CartItemModifier` writes); log when request has `SelectedModifiers`. Update-item: `SelectedModifiers` block only logs, no modifier DB updates. |
| **backend/Services/PaymentService.cs** | `requestedModifierIds` forced to empty for all items; log when request had modifier payload. |
| **backend/Services/TableOrderService.cs** | Removed loop that copied `CartItem.Modifiers` → `TableOrderItemModifier` in both Convert and Update. |
| **docs/architecture/addon-refactor/PHASE3_PREP_NO_LEGACY_MODIFIER_WRITES.md** | This migration note. |

---

## Compatibility notes

- **Clients still sending `SelectedModifiers` / `ModifierIds` / `Modifiers`:** Request is accepted; payload is ignored for write. No 4xx. Use logs to track remaining usage.
- **Historical carts with `CartItem.Modifiers`:** Continue to load and display; no new such rows created.
- **Historical payments with `PaymentItem.Modifiers`:** Receipt and totals unchanged.
- **Historical table orders with `TableOrderItemModifier`:** Recovery and display unchanged; no new rows written.
- **Add-ons:** Must be added as separate cart lines (add-item per add-on product) and separate payment items (productId of add-on product).

---

## Test updates

- **PaymentModifierValidationIntegrationTests:** CreatePayment_WithAllowedModifier → CreatePayment_WithModifierIds_Phase3IgnoresModifiers_TotalsProductOnly; CreatePayment_WithDisallowedModifier_Returns400 → CreatePayment_WithDisallowedModifierIds_Phase3Ignores_SucceedsWithProductOnly; CreatePayment_WithWrongPriceDelta_Returns400 → CreatePayment_WithWrongPriceDeltaInModifiers_Phase3Ignores_SucceedsWithProductOnly. All expect success and product-only totals.
- **Phase2ReceiptFlatTests:** GetReceiptData_FromPaymentWithLegacyModifiers_ReturnsNestedModifierLines → GetReceiptData_FromPaymentCreatedWithModifierIds_Phase3Ignores_ReturnsProductOnlyLine; asserts single receipt line, no modifier lines.

## Rollback

To re-enable legacy modifier writes: revert the edits in CartController (add-item and update-item), PaymentService, and TableOrderService to the previous behavior (restore validation and persist logic; restore TableOrderItemModifier copy loop). Revert the test renames/expectations above. No schema or DTO changes were made.
