# Phase C: POS Legacy Fallback Removal – Summary

POS no longer renders or processes legacy `group.modifiers` when `group.products` is empty. No UI fallback to `group.modifiers`, no mapping from modifier to pseudo-product UI items, no deprecated branch in selection UI.

---

## Removed fallback branches

| # | Location | What was removed |
|---|----------|------------------|
| 1 | `frontend/services/api/productService.ts` | **mapModifierGroup**: Stopped populating `modifiers` from API (`g.Modifiers ?? g.modifiers`). Now always sets `modifiers: []`. Catalog and product DTOs used by POS no longer receive legacy modifiers. |
| 2 | `frontend/services/api/productModifiersService.ts` | **getProductModifierGroups**: No longer returns raw API list. Added `mapGroupForPOS(g)` that normalizes each group with `modifiers: []` and only maps `products` from API. Cached and returned data for POS never contain legacy modifiers. |
| 3 | `frontend/components/ModifierSelectionModal.tsx` | **handleAdd**: Removed call to `onAdd([])`. Apply flow now only invokes `onAddAddOns(addOns)` when there are selected add-ons. `onAdd` is optional and no longer used in the selection UI. |

---

## Remaining legacy references in POS (intentional)

These remain for **cart/backend/historical compatibility**. They do not use `group.modifiers` for add-on selection or rendering; they handle **cart line** `item.modifiers` (existing orders, backend response, payment payload).

| # | File | Purpose |
|---|------|--------|
| 1 | `CartContext.tsx` | Cart line `item.modifiers`: backend response mapping, `addItem` request `selectedModifiers`, merge key `getModifierKey(item.modifiers)`, addModifier/incrementModifier/decrementModifier/removeModifier, line total. Needed for existing carts and backend contract. |
| 2 | `cash-register.tsx` | `lastCartItemModifiersByProductId`, `handleAddProduct(product, modifiers)`, `handleAddModifier`: support for legacy cart lines and inline chip state. |
| 3 | `CartDisplay.tsx` | `modifierKey` from `item.modifiers` for list key when itemId/clientId missing. |
| 4 | `CartItemRow.tsx` | Renders `item.modifiers` (name, price×qty, increment/decrement/remove) for lines that already have modifiers. |
| 5 | `PaymentModal.tsx` | `modifierIds` in payment payload for legacy lines. |
| 6 | `_layout.tsx` | Payment cartItems mapping: `modifiers: item.modifiers?.map(...)`. |
| 7 | `ReceiptLineItem.tsx`, `ReceiptSummary.tsx` | Receipt display of line-level modifiers. |
| 8 | Types | `ModifierGroupDto.modifiers`, `CartItem.modifiers`, `SelectedModifierInput`, payment `modifierIds`: kept for API/contract and backward compat. |

---

## Intentionally left for Phase D or later

| # | Item | Reason |
|---|------|--------|
| 1 | **ProductExtrasInline** | Not used in POS; displays `selectedModifiers` + Edit. Could be repurposed for “edit line” add-ons later. Not removed to avoid unrelated domain churn. |
| 2 | **ModifierSelectionModal** | Not mounted in current POS flow (bottom sheet is used). Kept for potential reuse; deprecated `onAdd` is optional and no longer called in apply. |
| 3 | **ModifierGroupDto.modifiers** type | Field kept in DTO; always empty in POS. Phase D can remove from API and type when backend drops it. |
| 4 | **Backend/Admin** | No change in backend or admin; legacy modifier fields and behavior remain where needed. |

---

## POS behavior after Phase C

- **Add-on source of truth**: Only `group.products` (add-on products). No UI or mapping uses `group.modifiers` for selection or display.
- **Catalog / modifier-group API**: Product and modifier-group DTOs in POS have `modifiers: []`; only `products` are populated.
- **Selection UI**: ModifierSelectionBottomSheet and ModifierSelectionModal use only `group.products`; no deprecated apply branch for legacy modifiers.
- **Cart**: New add-on flow adds base + add-on as flat lines. Existing cart lines with `item.modifiers` still display and sync for backward compatibility.
