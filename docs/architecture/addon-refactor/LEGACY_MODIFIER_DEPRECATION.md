# Legacy Modifier System – Deprecation Note

**Status:** Preparation phase. Legacy support preserved; no schema removal.

**Active model:** Add-on = Product (`IsSellableAddOn`, `AddOnGroupProduct`). Add-ons are normal products, appear as their own cart/order/receipt lines.

**Full removal:** Only after migration is complete and historical data is no longer needed for audit/receipt display.

---

## 1. Deprecation Summary

| Area | Status | Notes |
|------|--------|-------|
| **product_modifiers** table | Deprecated | Source for migration; new add-ons use Products |
| **ProductModifier** entity | Deprecated | Kept for read; ModifierMigrationService migrates to Products |
| **group.modifiers** (ModifierGroupDto) | Deprecated | Prefer group.products; still returned for legacy groups |
| **CartItemModifier** | Legacy read-only | No new writes; historical carts load correctly |
| **TableOrderItemModifier** | Legacy read-only | No new writes; historical table orders |
| **PaymentItem.Modifiers** | Legacy read-only | Historical payment snapshots; new payments flat |
| **SelectedModifiers** (add-item/update-item) | Deprecated | Accepted; ignored for write |

---

## 2. Deprecated Areas (Do Not Use for New Logic)

| Component | Location | Reason |
|-----------|----------|--------|
| `ProductModifier` | `backend/Models/ProductModifier.cs` | Add-ons = Product; use AddOnGroupProduct |
| `product_modifiers` table | DB schema | Migration target; new data in products |
| `CreateModifierRequest` / add-modifier endpoint | ModifierGroupsController | Use AddProductToGroup; creation frozen |
| `group.modifiers` for new UI | Frontend | Use group.products; modifiers = legacy fallback |
| `SelectedModifiers` in add-item | CartController | Ignored for write; send add-ons as separate add-item |
| `ModifierIds` in PaymentItemRequest | PaymentService | Ignored; add-ons = separate PaymentItem |
| `ProductModifierValidationService` modifier lookups | PaymentService | Block unreachable (requestedModifierIds always empty) |

---

## 3. Safe Future Cleanup Candidates

*Only after migration complete and no legacy carts/orders/receipts in production.*

| Candidate | Prerequisite | Risk |
|-----------|--------------|------|
| Remove `ProductModifier` writes from ModifierGroupsController | All modifiers migrated to Products | Low – migration service handles |
| Stop including `g.Modifiers` in GetProductModifierGroups response | All groups use Products only | Medium – legacy POS may still expect |
| Remove `SelectedModifiers` from AddItemToCartRequest | FE never sends it | Low – already ignored |
| Remove `CartItemModifier` table | No carts with modifiers in DB | High – requires data migration |
| Remove `TableOrderItemModifier` table | No table orders with modifiers | High – historical data |
| Remove `PaymentItem.Modifiers` deserialization | No payments with Modifiers in JSON | High – receipt display for old payments |
| Remove `ProductModifierValidationService` | No code path uses modifier validation | Low – PaymentService block is dead |

---

## 4. Current Blockers for Full Legacy Removal

| Blocker | Description |
|---------|-------------|
| **Historical carts** | Carts with `CartItemModifiers` must load; BuildCartResponse maps to SelectedModifiers |
| **Historical table orders** | TableOrderItemModifiers in DB; recovery API returns SelectedModifiers |
| **Historical payments** | PaymentDetails.PaymentItems JSON may contain Modifiers; receipt rendering uses them |
| **Historical receipts** | ReceiptItem.ParentItemId, IsModifierLine for nested modifier lines |
| **Modifier migration** | ModifierMigrationService reads ProductModifiers; migration must complete first |
| **Admin modifier-groups UI** | Still displays legacy modifiers; "Migrieren" creates Product, keeps modifier |
| **POS legacy fallback** | ProductRow/ProductGridCard show group.modifiers when group.products empty |
| **ProductModifierValidationService** | CartController/PaymentService inject it; PaymentService block is dead but service is used |

---

## 5. Routing New Logic Away from Legacy

| Flow | Current | Target |
|------|---------|--------|
| Add-on selection (POS) | group.products → addItem(productId) | ✅ Done |
| Catalog modifier groups | Include Products; Modifiers for legacy | ✅ Done |
| Cart add-item | No SelectedModifiers for add-ons | ✅ Done |
| Payment items | One PaymentItem per cart line | ✅ Done |
| Receipt | One ReceiptItem per PaymentItem | ✅ Done |
| Admin: add to group | AddProductToGroup; CreateModifier deprecated | ✅ Done |
| ModifierMigrationService | Reads ProductModifiers, creates Products | In progress |

---

## 6. File Reference

| Purpose | Path |
|---------|------|
| Deprecation note | `docs/architecture/addon-refactor/LEGACY_MODIFIER_DEPRECATION.md` |
| Validation checklist | `docs/architecture/addon-refactor/ADDON_VALIDATION_CHECKLIST.md` |
| Implementation plan | `docs/architecture/addon-refactor/ADDON_MODIFIER_IMPLEMENTATION_PLAN.md` |
| ProductModifier model | `backend/Models/ProductModifier.cs` |
| Modifier DTOs | `backend/DTOs/ModifierDTOs.cs` |
| CartController | `backend/Controllers/CartController.cs` |
| ModifierMigrationService | `backend/Services/ModifierMigrationService.cs` |
| ProductModifierValidationService | `backend/Services/ProductModifierValidationService.cs` |
| Frontend productModifiersService | `frontend/services/api/productModifiersService.ts` |
