# Phase D PR-B: selectedModifiers Write-Path Removal — Audit

**Goal:** Remove `selectedModifiers` from the active POS add-to-cart **write** path, while keeping historical and backend compatibility safe.

**Scope:** No backend breaking changes; response/read paths for legacy carts and table orders must remain.

---

## 1. Frontend: Where `selectedModifiers` Is **Written** Into Requests

| Location | What is written | Request |
|----------|------------------|--------|
| **`frontend/contexts/CartContext.tsx`** ~389–397 | `body.selectedModifiers = modifiers.map(m => ({ id: m.id, quantity: m.quantity ?? 1 }))` when `options.modifiers?.length` | `POST /cart/add-item` |
| **`frontend/contexts/CartContext.tsx`** ~712–716 | `SelectedModifiers: nextMods.map(...)` in PUT body | `PUT /Cart/items/{itemId}` (only from `persistModifiers`) |

**Active add-to-cart write path (the one to remove for PR-B):**

- **`frontend/app/(tabs)/cash-register.tsx`**  
  - `handleAddProduct(product, modifiers)` (lines 44–66) calls  
    `addItem(product.id, 1, { modifiers: withQty, productName, unitPrice })`.  
  - So whenever the product list calls `onAdd(product, pendingModifiers)` with non-empty `pendingModifiers` (from `selectedModifiersForProduct`), those modifiers are sent in the add-item request.
- **`frontend/contexts/CartContext.tsx`**  
  - In `addItem`, when `modifiers.length > 0`, it sets `body.selectedModifiers` (lines 396–397) and sends them to `POST /cart/add-item`.

So the **only** active POS path that still **writes** `selectedModifiers` on **add-to-cart** is:

1. **cash-register** → `handleAddProduct(product, modifiers)` with `modifiers` from `selectedModifiersForProduct` (last cart line or pending).
2. **CartContext.addItem** → `body.selectedModifiers` when `options.modifiers?.length`.

The **PUT** path (`persistModifiers`) is used only when editing **existing** cart lines (addModifier / incrementModifier / decrementModifier / removeModifier). Backend already ignores `SelectedModifiers` on PUT for write. PR-B can either leave PUT as-is (compat for legacy line editing) or stop sending `SelectedModifiers` on PUT for consistency; both are safe.

---

## 2. Frontend: Where `selectedModifiers` Is **Read**

| Location | Purpose |
|----------|--------|
| **`frontend/contexts/CartContext.tsx`** ~211 | `fetchTableCart`: `backendMods = item.SelectedModifiers ?? item.selectedModifiers ?? item.Modifiers ?? item.modifiers` → build `modifierList` for cart hydration. **Keep** (historical/legacy carts). |
| **`frontend/contexts/CartContext.tsx`** ~409 | `addItem` response mapping: same `item.SelectedModifiers ?? ...` → `modifierList` for local items. **Keep** (legacy response compat). |
| **`frontend/app/(tabs)/cash-register.tsx`** ~192–205 | `selectedModifiersForProduct`: derives display/pending state from `lastCartItemModifiersByProductId` and `pendingModifiersByProduct`. Used only to pass into `handleAddProduct` and to `ProductList` as `pendingModifiersByProduct`. **After PR-B:** can be simplified (no longer feed add-item write). |
| **`frontend/components/ProductRow.tsx`** ~89 | `selectedModifiers={[]}` passed to `ModifierOptionChips`. Read by chips for “selected” state; always empty in current flow. **Keep** (prop shape; no write). |
| **`frontend/components/ProductGridCard.tsx`** ~80 | Same: `selectedModifiers={[]}`. **Keep**. |
| **`frontend/components/ModifierOptionChips.tsx`** ~18, 34, 43 | Props `selectedModifiers: ModifierOptionItem[]`; used to build `selectedById` for rendering. **Keep** (display only; can stay `[]` from list). |

**Summary:** All **read** usages are either (a) mapping **backend response** for legacy carts/items (CartContext fetch + addItem response), or (b) display/pending state (cash-register, chips). For PR-B we **do not remove** response mapping; we only stop **sending** `selectedModifiers` on the **add-to-cart** request.

---

## 3. Backend: Where `SelectedModifiers` Is **Accepted** in Write Requests

| Location | Request | Behavior |
|----------|---------|----------|
| **`backend/Controllers/CartController.cs`** | | |
| ~236, 319–328 | `AddItemToCart([FromBody] AddItemToCartRequest request)` | Reads `request.SelectedModifiers`; normalizes; **does not write** CartItemModifiers (Phase 3). Logs `Phase2.LegacyModifier.AddItemRequestSelectedModifiers` when count > 0. |
| ~458, 491–497 | `UpdateCartItemSimple(itemId, [FromBody] UpdateCartItemRequest request)` | Reads `request.SelectedModifiers`; **does not write**; only logs when present. |
| ~515 | `UpdateCartItem(cartId, itemId, request)` | Same DTO; update logic does not persist SelectedModifiers. |
| **DTOs** ~1544–1571 | `AddItemToCartRequest.SelectedModifiers`, `UpdateCartItemRequest.SelectedModifiers` | Optional; marked obsolete/deprecated in comments. |

**Conclusion:** Backend **accepts** but **ignores** `SelectedModifiers` on both add-item and update-item. No backend change required for PR-B; we only stop sending from frontend.

---

## 4. Backend: Where `SelectedModifiers` Is **Returned** in Responses

| Location | Response | Purpose |
|----------|----------|--------|
| **`CartController.BuildCartResponse`** ~1460–1494 | Cart response items | Maps `ci.Modifiers` → `SelectedModifiers` per line. **Must remain** for legacy carts with CartItemModifiers. |
| **Table order recovery** ~1214, 1288 | TableOrderRecoveryResponse items | Maps `TableOrderItemModifiers` / `CartItem.Modifiers` → `SelectedModifiers`. **Must remain** for historical table orders. |

**Conclusion:** All **response** usages are for **historical compatibility** (carts/table orders that already have modifier rows). **Do not remove** from backend.

---

## 5. Does Any Active POS Create-Flow Still Depend on `selectedModifiers`?

**Yes, one path still uses it for create (add-to-cart):**

- **Product list “add with modifiers” path (legacy inline):**  
  - User taps a product **row** (no add-on sheet).  
  - `ProductRow` / `ProductGridCard` call `onAdd(product, pendingModifiers)`.  
  - `pendingModifiers` = `selectedModifiersForProduct[product.id]` (from last cart line for that product or `pendingModifiersByProduct`).  
  - `handleAddProduct(product, modifiers)` then calls `addItem(product.id, 1, { modifiers: withQty, ... })`.  
  - So **new** lines can still be created **with** `body.selectedModifiers` when the user had previously selected modifiers (or last line had them).

**All other create paths already do not send modifiers:**

- **Add-on sheet:** `handleApplyWithBase` → `addItemWithAddOns` → `addItem(..., { productName, unitPrice })` only (no modifiers).
- **Chip add-on:** `handleAddAddOn` → `addItem(..., { productName, unitPrice })` only.
- **CartScreen:** `handleAddItem` → `addItem(productId, quantity)` (no options).
- **addToCart adapter:** `addItem(item.productId, item.quantity)` (no options).

So the **only** active create-flow that still depends on sending `selectedModifiers` is the **row tap without sheet** when `pendingModifiers` is non-empty. PR-B removes that dependency by no longer sending `selectedModifiers` on add-item from POS.

---

## 6. What Must Remain for Historical Compatibility

| Area | What to keep |
|------|--------------|
| **Backend responses** | `SelectedModifiers` on cart items and table-order recovery items (BuildCartResponse, recovery DTOs). Legacy carts/table orders with modifier rows must still load and display. |
| **Frontend cart hydration** | CartContext: reading `item.SelectedModifiers ?? item.selectedModifiers ?? ...` in `fetchTableCart` and in `addItem` response mapping → `modifierList` / `item.modifiers`. |
| **Frontend types** | `CartItem.modifiers`, `AddItemToCartRequest.selectedModifiers?` (optional) in cartService and CartContext so that (a) backend can still send modifiers in responses, (b) type contracts don’t break if something still passes options. |
| **Backend request DTOs** | Keeping `SelectedModifiers` on `AddItemToCartRequest` and `UpdateCartItemRequest` as optional is safe (backward compat; other clients or future use). No need to remove in PR-B. |
| **CartDisplay / modifier actions** | `addModifier`, `incrementModifier`, `decrementModifier`, `removeModifier` and CartDisplay UI for existing lines with modifiers (legacy cart editing). Optional: keep sending `SelectedModifiers` in `persistModifiers` PUT or stop; backend ignores it either way. |

---

## Touched File Candidates (PR-B)

| File | Change (candidate) |
|------|---------------------|
| **`frontend/contexts/CartContext.tsx`** | In `addItem`, **stop setting** `body.selectedModifiers` (remove or gate the block that sets it when `modifiers.length > 0`). Keep response mapping and `getModifierKey` / line totals / existing modifier handlers. |
| **`frontend/app/(tabs)/cash-register.tsx`** | **Stop passing modifiers into add-item:** e.g. `handleAddProduct(product, modifiers)` → call `addItem(product.id, 1, { productName, unitPrice })` only (ignore `modifiers` for the request). Optionally simplify to `handleAddProduct(product)` and drop `selectedModifiersForProduct` from the add path (can keep for display only). |
| **`frontend/services/api/cartService.ts`** | **Optional:** Keep `selectedModifiers?: SelectedModifierInput[]` on `AddItemToCartRequest` for type/backend compat; add comment that POS no longer sends it (Phase D PR-B). |

**Not touched in PR-B (or optional):**

- **Backend:** No change; keep accepting and returning `SelectedModifiers` as today.
- **CartContext `persistModifiers`:** Can leave as-is (still send `SelectedModifiers` on PUT for legacy line editing) or stop sending; both are safe since backend ignores.
- **ProductRow / ProductGridCard / ModifierOptionChips:** Keep `selectedModifiers={[]}` and current props; no write path change there.
- **CartScreen:** Already does not pass modifiers; no change.

---

## Active Write-Path Usages (to remove in PR-B)

1. **CartContext.addItem:** Remove or gate the block that sets `body.selectedModifiers` when `options.modifiers?.length` (so POS add-item never sends selectedModifiers).
2. **cash-register handleAddProduct:** Stop passing `modifiers` into `addItem` (call `addItem(product.id, 1, { productName, unitPrice })` only).

---

## Read / Compatibility Usages (keep)

- CartContext: `fetchTableCart` and `addItem` response mapping of `SelectedModifiers` / `selectedModifiers` → `modifierList` / `item.modifiers`.
- CartContext: `getModifierKey`, `getCartLineTotal`, `recalcLineTotal`, `lineKey`, addModifier/incrementModifier/decrementModifier/removeModifier, `persistModifiers` (logic for existing lines).
- cash-register: `selectedModifiersForProduct` can remain for display/pending state; it just must not feed into the add-item request body.
- ProductRow / ProductGridCard: `selectedModifiers={[]}` and ModifierOptionChips `selectedModifiers` prop (read/display).
- Backend: BuildCartResponse and table-order recovery returning `SelectedModifiers`; AddItemToCartRequest/UpdateCartItemRequest accepting (but ignoring) `SelectedModifiers`.

---

## Risky Areas

| Risk | Mitigation |
|------|------------|
| **Legacy carts with modifiers** | Do not remove **reading** of `SelectedModifiers` in cart fetch or addItem response; do not remove `item.modifiers` or line total logic. |
| **Table order recovery** | Do not change backend response shape; keep `SelectedModifiers` on recovery items. |
| **CartScreen or other callers** | CartScreen already calls `addItem(productId, quantity)` with no options; no change. If any other code ever passed `options.modifiers`, after PR-B that would simply no longer be sent to the backend (backend already ignores). |
| **Type/contract break** | Keep `AddItemToCartRequest.selectedModifiers` optional in frontend types so existing types and backend contract remain valid. |

---

## Minimal Safe Implementation Plan for PR-B

1. **CartContext (`frontend/contexts/CartContext.tsx`)**  
   - In `addItem`, **remove** the block that sets `body.selectedModifiers` when `modifiers.length > 0` (lines ~396–397).  
   - Optionally keep `options.modifiers` for optimistic local state if desired (e.g. merge key), but **do not** send them in the request.  
   - Leave all response mapping (`item.SelectedModifiers ?? ...` → `modifierList`), `getModifierKey`, line totals, and modifier handlers unchanged.

2. **cash-register (`frontend/app/(tabs)/cash-register.tsx`)**  
   - In `handleAddProduct`, call `addItem(product.id, 1, { productName: product.name, unitPrice: product.price ?? 0 })` **only** (do not pass `modifiers` into the request).  
   - Signature can remain `handleAddProduct(product, modifiers)` for compatibility with `onAdd(product, pendingModifiers)`; the second argument is simply not used for the API call.  
   - Optional: simplify to `handleAddProduct(product)` and update ProductList/ProductRow/ProductGridCard to pass only product; or leave as-is and only change the addItem call.

3. **cartService (`frontend/services/api/cartService.ts`)**  
   - Keep `selectedModifiers?: SelectedModifierInput[]` on `AddItemToCartRequest`.  
   - Add a one-line comment: e.g. “POS does not send selectedModifiers (Phase D PR-B); kept for backend/type compat.”

4. **Backend**  
   - No changes. Keep accepting `SelectedModifiers` on add-item and update-item; keep returning them in cart and table-order recovery responses.

5. **Verification**  
   - After changes: add product from POS (row tap, with or without add-on sheet); confirm request body has no `selectedModifiers` (or empty array).  
   - Load a legacy cart that has lines with modifiers; confirm cart still hydrates and displays modifiers.  
   - Run existing Phase2 tests (e.g. add-item with/without SelectedModifiers, table order recovery with modifiers).

This yields a **minimal, safe** removal of `selectedModifiers` from the active POS add-to-cart write path while preserving historical and backend compatibility.
