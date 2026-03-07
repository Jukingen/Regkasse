# Phase C: POS Add-on Refactor – Final Summary

## Final touched files (cleanup pass)

| File | Changes |
|------|--------|
| `frontend/components/ModifierSelectionBottomSheet.tsx` | English comment for `onApplyWithBase`; `getSelectedAddOns` iterates only groups with products; `handleApply` validation simplified (`!validation.valid` then use first error message). |
| `frontend/contexts/CartContext.tsx` | Comments for `addItemWithAddOns` and type definition translated to English (base + add-ons, flat cart, no parent_product_id in backend). |
| `frontend/app/(tabs)/cash-register.tsx` | Header comment updated: add-on sheet flow for products with `group.products`, direct add for others, legacy chip path noted. |
| `frontend/components/ProductRow.tsx` | Prop comments: `onAddAddOn` and `onOpenAddOnSheet` in English. |
| `frontend/components/ProductGridCard.tsx` | Prop comment: `onOpenAddOnSheet` in English. |
| `frontend/components/ProductList.tsx` | Prop comment: `onAddAddOn` in English. |

---

## Final architecture summary

### Add-on source of truth (POS Phase C)

- **Catalog / API:** Only `group.products` drive add-on UI. `group.modifiers` is never populated for POS (productService `mapModifierGroup` sets `modifiers: []`; productModifiersService `getProductModifierGroups` normalizes via `mapGroupForPOS` with `modifiers: []`).
- **Product list:** `groupsWithProducts = groups.filter(g => (g.products ?? []).length > 0)`. Products with no add-on groups add directly; products with add-on groups open `ModifierSelectionBottomSheet` when `onOpenAddOnSheet` is provided.
- **Bottom sheet:** Renders options from `group.products` only. Validation via `modifierSelectionUtils` (required single-select, multi-select max, disabled state). On Fertig: `onApplyWithBase(base, addOns)` → `addItemWithAddOns(baseProductId, baseProductName, baseUnitPrice, addOns)`.
- **Cart:** Flat. One line for base product (no modifiers), one line per selected add-on. No `parent_product_id` in backend; order of lines implies association. Legacy cart lines with `item.modifiers` still supported for existing data and payment payload.

### Data flow

1. **Tap product without add-on groups** → `onAdd(product, pendingModifiers)` → `addItem(productId, 1, { productName, unitPrice, modifiers? })` (legacy path when chips used).
2. **Tap product with add-on groups** → `onOpenAddOnSheet(product)` → set `modifierSheetProduct` → render `ModifierSelectionBottomSheet` with `onApplyWithBase={handleApplyWithBase}`.
3. **Fertig** → `validateAllGroups`; if invalid, show first error and block; if valid, `addItemWithAddOns(base, addOns)` → one `addItem` for base, one per add-on (no modifiers).

### Key files

| Area | Files |
|------|--------|
| Selection logic | `frontend/utils/modifierSelectionUtils.ts` |
| API / DTOs | `frontend/services/api/productService.ts` (mapModifierGroup), `productModifiersService.ts` (mapGroupForPOS, getProductModifierGroups) |
| POS UI | `ModifierSelectionBottomSheet.tsx`, `ProductRow.tsx`, `ProductGridCard.tsx`, `ProductList.tsx`, `cash-register.tsx` |
| Cart | `CartContext.tsx` (addItem, addItemWithAddOns) |

### What POS does not use

- No UI fallback to `group.modifiers`.
- No mapping from legacy modifier to pseudo-product for add-on display.
- No deprecated submit branch in selection UI (primary path is `onApplyWithBase`).

---

## Follow-up items for Phase D

| # | Item | Notes |
|---|------|--------|
| 1 | **Backend: optional parent_product_id** | Add to CartItem / AddItemToCartRequest if product hierarchy should be persisted; frontend can then send it for add-on lines. |
| 2 | **Remove ModifierGroupDto.modifiers from API** | Once backend stops returning `modifiers` on groups, remove from DTO and from productModifiersService/Product type. |
| 3 | **ModifierSelectionModal** | Currently unused in POS (bottom sheet is used). Remove or repurpose; drop deprecated `onAdd` when no callers remain. |
| 4 | **ProductExtrasInline** | Not used in POS. Remove if no “edit line” use case, or repurpose for add-on-only edit UI. |
| 5 | **Legacy cart path** | Consider deprecating `handleAddProduct(..., modifiers)` and `handleAddModifier` once all flows use add-on-as-product; keep cart/backend handling of `item.modifiers` for historical orders and payment payload until API contract changes. |
| 6 | **Tests** | Existing: `posModifierFlow.test.ts`, `modifierSelectionUtils.test.ts`, `addOnFlow.test.ts`. Phase D: add integration test for full sheet → cart flow if needed. |
