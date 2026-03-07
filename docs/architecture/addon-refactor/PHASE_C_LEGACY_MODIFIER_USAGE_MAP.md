# Phase C: Legacy POS Modifier Usage Map

Precise list of every place in the POS (frontend) where legacy modifiers are still used or assumed.  
**Do not refactor yet.** Use this for Phase C removal/replacement planning.

For each usage:
- **Category:** rendering | mapping | validation | typing | cart logic
- **Recommendation:** remove | replace | keep temporarily

---

## 1. group.modifiers (catalog / modifier group DTO)

| # | File | Function / Component | Line(s) | Category | Description | Recommendation |
|---|------|----------------------|---------|----------|--------------|-----------------|
| 1.1 | `frontend/services/api/productService.ts` | `mapModifierGroup` | 142–148 | **mapping** | Reads `g.Modifiers ?? g.modifiers` and maps into `ModifierGroupDto.modifiers` for catalog and modifier-groups API responses. Only place in POS that populates group-level legacy modifiers. | **remove** (for catalog path: set `modifiers: []` or omit; Phase D removes from API) |
| 1.2 | `frontend/services/api/productModifiersService.ts` | (file comment + type) | 4–5, 36 | **typing** | `ModifierGroupDto.modifiers: ModifierDto[]` and comment "LEGACY: group.modifiers = deprecated". Type is still in API response. | **keep temporarily** (mark deprecated in JSDoc; remove property in Phase D when API omits it) |

---

## 2. product.modifierGroups (catalog shape)

| # | File | Function / Component | Line(s) | Category | Description | Recommendation |
|---|------|----------------------|---------|----------|--------------|-----------------|
| 2.1 | `frontend/components/ProductRow.tsx` | `ProductRowInner` | 46–52 | **rendering** | Reads `product.modifierGroups ?? []`, filters `groupsWithProducts = (g.products ?? []).length > 0` only. Does **not** read group.modifiers. | **keep** (already products-only) |
| 2.2 | `frontend/components/ProductGridCard.tsx` | `ProductGridCardInner` | 46–51 | **rendering** | Same as 2.1. | **keep** (already products-only) |
| 2.3 | `frontend/components/ModifierSelectionBottomSheet.tsx` | (props + effect) | 40–41, 56, 77–78, 103 | **rendering** | Accepts `modifierGroups?: ModifierGroupDto[]`; uses only `group.products` for options. Component not mounted anywhere in POS. | **keep** (or remove if component deleted) |
| 2.4 | `frontend/components/ModifierSelectionModal.tsx` | (props + effect) | 39–40, 53, 71–72, 95 | **rendering** | Same as 2.3. | **keep** (or remove if component deleted) |

---

## 3. Fallback rendering (group.products vs group.modifiers)

| # | File | Function / Component | Line(s) | Category | Description | Recommendation |
|---|------|----------------------|---------|----------|--------------|-----------------|
| 3.1 | `frontend/components/ModifierSelectionBottomSheet.tsx` | render | 175, 195 | **rendering** | `groupsWithProducts = groups.filter((g) => (g.products ?? []).length > 0)`; only renders `(group.products ?? []).map(...)`. No fallback to group.modifiers. | **keep** (no legacy fallback) |
| 3.2 | `frontend/components/ModifierSelectionModal.tsx` | render | 166, 186 | **rendering** | Same as 3.1. | **keep** (no legacy fallback) |
| 3.3 | `frontend/hooks/useProductModifierGroups.ts` | `useProductModifierGroups` | 48 | **rendering** | `hasModifiers = groups.some((g) => (g.products?.length ?? 0) > 0)`. Products-only; no modifiers fallback. | **keep** (no legacy fallback) |

**Note:** The audit doc referred to old fallbacks (e.g. "if products.length === 0 use group.modifiers"). Current code has **no** such branch; all add-on display is from `group.products` only.

---

## 4. Legacy modifier payload mapping (API request / response)

| # | File | Function / Component | Line(s) | Category | Description | Recommendation |
|---|------|----------------------|---------|----------|--------------|-----------------|
| 4.1 | `frontend/contexts/CartContext.tsx` | `fetchTableCart` (backend → local) | 208, 219–221 | **mapping** | Reads `item.SelectedModifiers ?? item.selectedModifiers ?? item.Modifiers ?? item.modifiers` from backend item; builds `modifierList`; fallback `existing?.modifiers` when backend has no mods. | **keep temporarily** (backend still returns selectedModifiers for legacy lines; needed for cart hydration) |
| 4.2 | `frontend/contexts/CartContext.tsx` | `addItem` (response mapping) | 406, 419–421 | **mapping** | Same pattern after POST add-item: backend item mods or fallback to `existing?.modifiers`. | **keep temporarily** (same as 4.1) |
| 4.3 | `frontend/contexts/CartContext.tsx` | `addItem` (request body) | 393–394 | **mapping** | `body.selectedModifiers = modifiers.map(m => ({ id: m.id, quantity: m.quantity ?? 1 }))` when adding with legacy modifiers. | **keep temporarily** (backend still accepts selectedModifiers) |
| 4.4 | `frontend/contexts/CartContext.tsx` | `persistModifiers` | 700 | **mapping** | `SelectedModifiers: nextMods.map((m) => ({ Id: m.id, Quantity: m.quantity ?? 1 }))` for PUT cart item. | **keep temporarily** (backend contract) |
| 4.5 | `frontend/app/(tabs)/_layout.tsx` | PaymentModal cartItems mapping | 126 | **mapping** | `modifiers: item.modifiers?.map(m => ({ modifierId: m.id, name: m.name, priceDelta: m.price }))` — cart line modifiers → payment payload shape. | **keep temporarily** (payment/receipt still expect modifierId for legacy lines) |
| 4.6 | `frontend/components/PaymentModal.tsx` | (cartItems type) | 108–109 | **typing** | `modifiers?: Array<{ modifierId: string; name?: string; priceDelta?: number }>`. | **keep temporarily** (payment API shape) |
| 4.7 | `frontend/components/PaymentModal.tsx` | payment request build | 281, 286 | **mapping** | Comment: "Add-on lines have no modifiers; legacy lines may have modifierIds (deprecated)". `modifierIds: (item.modifiers?.length ? (item.modifiers as any[]).map((m: any) => m.id ?? m.modifierId).filter(Boolean) : undefined)`. | **keep temporarily** (backend payment API still accepts modifierIds for legacy) |
| 4.8 | `frontend/services/api/cartService.ts` | `AddItemToCartRequest` | 61–62 | **typing** | `selectedModifiers?: SelectedModifierInput[]`. | **keep temporarily** (backend contract) |
| 4.9 | `frontend/services/api/paymentService.ts` | `PaymentItem` | 12, 18 | **typing** | Comment "Add-ons = product-only (no modifierIds)"; `modifierIds?: string[]`. | **keep temporarily** (backend payment contract) |

---

## 5. Transformation logic (modifiers → UI items)

| # | File | Function / Component | Line(s) | Category | Description | Recommendation |
|---|------|----------------------|---------|----------|--------------|-----------------|
| 5.1 | `frontend/services/api/productService.ts` | `mapModifierGroup` | 142–148 | **mapping** | Converts API `g.Modifiers ?? g.modifiers` into `ModifierDto[]` (id, name, price, taxType, sortOrder). Feeds catalog product.modifierGroups. | **remove** (for catalog: stop populating modifiers; see 1.1) |
| 5.2 | `frontend/components/ProductRow.tsx` | `ProductRowInner` | 84–86 | **rendering** | Passes `(group.products ?? []).map((p) => ({ id: p.productId, name: p.productName, price: p.price }))` to ModifierOptionChips as `modifiers` prop; `selectedModifiers={[]}`. Add-ons rendered as chips; no legacy modifier transformation. | **keep** (products → chip items) |
| 5.3 | `frontend/components/ProductGridCard.tsx` | `ProductGridCardInner` | 71–74 | **rendering** | Same as 5.2. | **keep** |
| 5.4 | `frontend/components/ModifierOptionChips.tsx` | (props) | 16–18, 33–34, 43, 54, 60 | **rendering** | Receives `modifiers` and `selectedModifiers` (both ModifierOptionItem). Used with product add-ons (id = productId). Legacy path would pass modifier ids. | **keep** (shape works for add-on products; no change for Phase C) |

---

## 6. Cart logic (modifier vs product add-on distinction)

| # | File | Function / Component | Line(s) | Category | Description | Recommendation |
|---|------|----------------------|---------|----------|--------------|-----------------|
| 6.1 | `frontend/app/(tabs)/cash-register.tsx` | `usePOSOrderFlow` | 47–71 | **cart logic** | `handleAddProduct(product, modifiers)`: adds one line with `modifiers` (legacy). | **keep temporarily** (still used when row tap includes pending legacy modifiers; can deprecate after full add-on-as-product flow) |
| 6.2 | `frontend/app/(tabs)/cash-register.tsx` | `usePOSOrderFlow` | 73–106 | **cart logic** | `handleAddModifier(product, modifier)`: adds modifier to existing line or new line with one modifier (legacy path). | **keep temporarily** (chips can still be legacy modifier id if any UI fed it; ProductRow/Grid now feed productId only to onAddAddOn) |
| 6.3 | `frontend/app/(tabs)/cash-register.tsx` | `usePOSOrderFlow` | 108–126 | **cart logic** | `handleAddAddOn(addOn)`: adds one line with `addItem(productId, 1, { productName, unitPrice })` — no modifiers (add-on as product). | **keep** (target pattern) |
| 6.4 | `frontend/app/(tabs)/cash-register.tsx` | (derived state) | 214–224, 237–252, 453 | **cart logic** | `lastCartItemModifiersByProductId`: from cart items, `map[productId] = item.modifiers ?? []`. Used for chip selected state (last line per product). Distinguishes "line has modifiers" for display. | **keep temporarily** (supports legacy lines with modifiers; add-on-only lines have no modifiers) |
| 6.5 | `frontend/contexts/CartContext.tsx` | `getModifierKey` | 309–315 | **cart logic** | Builds key from `item.modifiers` (id:quantity sorted) for merge/dedup: same product + same modifier set = same line. | **keep temporarily** (needed while cart can have legacy modifier lines) |
| 6.6 | `frontend/contexts/CartContext.tsx` | `addItem` | 332, 341–342 | **cart logic** | Uses `modKey = getModifierKey(modifiers)`; finds existing by `productId` + `getModifierKey(item.modifiers)`. Merges qty when same product+modifier set. | **keep temporarily** (same as 6.5) |
| 6.7 | `frontend/contexts/CartContext.tsx` | `removeByItemId` / `updateItemQuantityByItemId` | 590, 641 | **cart logic** | `lineKey = i.itemId ?? i.clientId ?? \`${i.productId}-${getModifierKey(i.modifiers)}\`` to identify item when backend id missing. | **keep temporarily** (supports legacy lines) |
| 6.8 | `frontend/contexts/CartContext.tsx` | `getCartLineTotal` (export) | 42–46 | **cart logic** | Line total = base + `(item.modifiers ?? []).reduce(...)`. | **keep temporarily** (legacy lines have modifiers on line) |
| 6.9 | `frontend/contexts/CartContext.tsx` | `addItem` (optimistic total) | 349, 371 | **cart logic** | Uses `item.modifiers` for line total and stores `modifiers` on new item. | **keep temporarily** |
| 6.10 | `frontend/contexts/CartContext.tsx` | `recalcLineTotal` | 688–690 | **cart logic** | Includes `(it.modifiers ?? []).reduce(...)` in line total. | **keep temporarily** |
| 6.11 | `frontend/contexts/CartContext.tsx` | `addModifier`, `incrementModifier`, `decrementModifier`, `removeModifier` | 691–707, 709–821, 742–805 | **cart logic** | Full CRUD for modifiers on a cart line (persistModifiers, nextMods, filter by modifierId). | **keep temporarily** (backend and existing carts support line modifiers) |
| 6.12 | `frontend/components/CartDisplay.tsx` | render list | 99 | **cart logic** | `modifierKey = (item.modifiers ?? []).map(m => m.id).sort().join(',')` for stable key when itemId/clientId missing. | **keep temporarily** |
| 6.13 | `frontend/components/CartItemRow.tsx` | render | 51–105 | **rendering** | Renders `item.modifiers` (name, price×qty, increment/decrement/remove). Distinguishes "line with modifiers" for UI. | **keep temporarily** (legacy lines show modifiers; add-on lines have no modifiers) |

---

## 7. Receipt / payment display (modifiers on line)

| # | File | Function / Component | Line(s) | Category | Description | Recommendation |
|---|------|----------------------|---------|----------|--------------|-----------------|
| 7.1 | `frontend/components/ReceiptLineItem.tsx` | `ReceiptLineItem` | 15, 20, 38–40 | **rendering** | Props `modifiers?: ReceiptItemDTO[]`; renders nested modifier lines. Comment: "Phase 2 flat = one main line per product (modifiers empty). Legacy = main + modifier lines." | **keep temporarily** (receipt can still have legacy structure) |
| 7.2 | `frontend/components/ReceiptSummary.tsx` | (grouping + render) | 45, 48, 58, 62, 83–85 | **rendering** | Groups items; `modifiers` on group; pushes legacy modifier lines into `last.modifiers`; renders `item.modifiers.length` and map. | **keep temporarily** (same as 7.1) |

---

## 8. Typing only (no runtime legacy path in POS selection)

| # | File | Type / Export | Line(s) | Category | Description | Recommendation |
|---|------|----------------|---------|----------|--------------|-----------------|
| 8.1 | `frontend/services/api/productModifiersService.ts` | `ModifierGroupDto.modifiers` | 36 | **typing** | DTO has `modifiers: ModifierDto[]`. | **keep temporarily** (Phase D: remove when API omits) |
| 8.2 | `frontend/contexts/CartContext.tsx` | `CartItem.modifiers` | 38 | **typing** | `modifiers?: CartItemModifier[]` on cart line. | **keep temporarily** (cart line state for legacy and backward compat) |
| 8.3 | `frontend/contexts/CartContext.tsx` | `ModifierSelection` | 19–20 | **typing** | Payment payload shape. | **keep temporarily** |
| 8.4 | `frontend/components/ModifierSelectionModal.tsx` | `onAdd(selectedModifiers)` | 43 | **typing** | Legacy callback; modal currently calls `onAdd([])` and uses onAddAddOns for products. | **replace** (when cleaning modal: drop onAdd or keep for empty only) |

---

## 9. Tests

| # | File | Describe / it | Line(s) | Category | Description | Recommendation |
|---|------|----------------|---------|----------|--------------|-----------------|
| 9.1 | `frontend/__tests__/addOnFlow.test.ts` | (test data + logic) | 21, 34, 52–102, 144–185 | **validation** | Local `ModifierGroupDto` with `modifiers`; `groupsWithProducts` / `hasModifiers` products-only; `getCartLineTotal` with `item.modifiers`. Tests legacy "group with only modifiers" returns false and cart line total with modifiers. | **keep** (tests products-only display + legacy line total; remove or relax `modifiers` in test DTOs when type is simplified in Phase D) |

---

## 10. Unused / optional components

| # | File | Component | Category | Description | Recommendation |
|---|------|------------|----------|--------------|-----------------|
| 10.1 | `frontend/components/ProductExtrasInline.tsx` | `ProductExtrasInline` | **rendering** | Uses `selectedModifiers` and `ExtrasChips(modifiers=selectedModifiers)`. Not imported anywhere in POS. Generic "extras" display (could be add-on products or legacy modifiers by shape). | **remove** (dead code) or **keep** if future "Edit line" reuses it |
| 10.2 | `frontend/components/ModifierSelectionBottomSheet.tsx` | `ModifierSelectionBottomSheet` | **rendering** | Not mounted in cash-register or ProductList. Already products-only. | **keep** (for future "Edit" flow) or **remove** (dead code) |
| 10.3 | `frontend/components/ModifierSelectionModal.tsx` | `ModifierSelectionModal` | **rendering** | Same as 10.2; has legacy `onAdd(selectedModifiers)` in props. | Same as 10.2; **replace** onAdd with add-on-only when cleaning |

---

## Summary by recommendation

| Recommendation | Count | Where |
|----------------|-------|--------|
| **remove** | 2 | productService `mapModifierGroup` modifiers population (catalog); optionally ProductExtrasInline (dead) |
| **replace** | 1 | ModifierSelectionModal `onAdd` when simplifying to add-on-only |
| **keep** | 12+ | ProductRow, ProductGridCard, useProductModifierGroups, ModifierOptionChips, ModifierSelection* (products-only rendering); addOnFlow.test |
| **keep temporarily** | 25+ | All cart/payment/receipt logic and mapping that handles `item.modifiers` or `selectedModifiers` / `modifierIds` (backend and legacy carts); types ModifierGroupDto.modifiers, CartItem.modifiers, payment types |

---

*Generated for Phase C planning. Do not refactor until Phase C implementation is approved.*
