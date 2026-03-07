# Add-on-as-Product Refactor — Final State

**Status:** Production. End state after Phase C and Phase D (PR-A, PR-B, PR-C, PR-D).

---

## 1. Final architecture

- **Add-ons are products.** Each selectable “extra” (e.g. Extra Käse) is a **Product** with `IsSellableAddOn = true`, linked to a **ProductModifierGroup** via **AddOnGroupProduct**.
- **Modifier groups** expose two shapes:
  - **Products** (primary): suggested add-on products (id, name, price, tax). Used for POS catalog, product form, and all new flows.
  - **Modifiers** (legacy): old `ProductModifier` entities. Used only for admin migration and historical display; no new creation.
- **Cart and payment are flat:** one cart line per product (base or add-on). No embedded “modifier” lines on the write path. Legacy DB/JSON with embedded modifiers is still **read** for receipts, recovery, and display.
- **Admin:** Product edit uses **assigned group IDs** from `GET /api/admin/products/{id}/modifier-groups` (products-only). Full group data (including legacy modifiers) comes from `GET /api/modifier-groups` for the modifier-groups page and migration.

---

## 2. POS runtime behavior

| Flow | Behavior |
|------|----------|
| **Catalog** | Categories and products; each product has `modifierGroups[]` with **products** only (POS endpoints return empty `modifiers`). |
| **Add to cart** | One tap = one product. Base + add-ons = N separate `addItem(productId, 1, { productName?, unitPrice? })` calls. Request body: `productId`, `quantity`, `tableNumber` only — **no** `selectedModifiers`. |
| **Cart display** | One line per cart item. New data: lines have no embedded modifiers. Legacy data: if API returns `SelectedModifiers`, UI shows them (CartItemRow, ReceiptSummary, totals). |
| **Payment** | One `PaymentItem` per cart line (`productId`, `quantity`, `taxType`). **No** `modifierIds` or modifier payload sent. |
| **Receipt** | New payments: one receipt line per payment item. Old payments: receipt service still reads `PaymentItem.Modifiers` from JSON and emits main + nested modifier lines for reprint/history. |

POS does not use `group.modifiers` for display or add-to-cart, does not send `selectedModifiers` on add-item, and does not send `modifierIds` in payment.

---

## 3. Cart model

- **Logical model:** Cart has items. Each item = one product (base or add-on): `productId`, `quantity`, `unitPrice`, line total. Add-ons are separate items.
- **Storage:** `cart_items` (productId, quantity, unitPrice, notes). `cart_item_modifiers` exists for **legacy** rows only; no new rows written. Cart GET and recovery still load and map `CartItemModifier` → `SelectedModifiers` for display/totals when present.
- **Table orders:** Same idea. `table_order_items`; `table_order_item_modifiers` for legacy only. No new TableOrderItemModifier rows; conversion and recovery read cart/table Modifiers for totals and response shape.
- **Merge rule:** Same productId, no legacy modifiers on line → merge quantity. Otherwise distinct lines.

---

## 4. API contract split

### POS (products-only)

| Endpoint | Modifier groups response | Use |
|----------|---------------------------|-----|
| GET /api/products/catalog | `modifierGroups[].products` populated; `modifierGroups[].modifiers` empty | Catalog, add-on chips |
| GET /api/products/{id}/modifier-groups | Same (products-only, modifiers empty) | Product “Extra Zutaten” in POS |

Add-item and payment requests do not send `selectedModifiers` or `modifierIds` from POS.

### Admin

| Endpoint | Modifier groups response | Use |
|----------|---------------------------|-----|
| GET /api/modifier-groups, GET /api/modifier-groups/{id} | Full `ModifierGroupDto`: **products** + **modifiers** | List, migration (“Als Produkt migrieren”), ExtraZutatenSection legacy block |
| GET /api/admin/products/{id}/modifier-groups | Products populated; **modifiers empty** | Assigned group IDs only in product form |
| POST …/modifier-groups/{id}/modifiers/{modifierId}/migrate | Migrate legacy modifier to product | Migration flow |

### Historical compatibility (read-only)

| Area | Contract | Purpose |
|------|----------|---------|
| Cart | GET cart includes `CartItem.Modifiers`; response `SelectedModifiers` per item | Legacy carts display and totals |
| Recovery | GET table-orders-recovery includes cart/table Modifiers → `SelectedModifiers` | F5 recovery for legacy orders |
| Payment | Payment JSON may contain `PaymentItem.Modifiers`; receipt services read it | Reprint and history for old receipts |
| Request | AddItemToCartRequest / UpdateCartItemRequest may carry `SelectedModifiers`; backend accepts, **does not write** | Backward compat |

---

## 5. What was removed

- **POS add-item:** Sending `selectedModifiers` on POST /cart/add-item (Phase D PR-B).
- **POS payment:** Sending `modifierIds` / modifier payload in payment request (Phase D PR-A).
- **POS UI:** Runtime use of `group.modifiers` for add-on options; add-on chips use `group.products` only (Phase C).
- **Components:** ModifierSelectionModal, ProductExtrasInline (removed; no longer in repo).
- **Backend write paths:** No new CartItemModifier or TableOrderItemModifier rows; no new PaymentItem.Modifiers in payment JSON. Legacy modifier creation (POST …/modifiers) returns 410; admin stub documents this.

---

## 6. What was intentionally kept

- **Admin:** Full `Modifiers` on GET /api/modifier-groups for migration UI and “Modifier (Legacy, nur Leseansicht)” in ExtraZutatenSection.
- **Admin:** migrateLegacyModifier and ModifierMigrationService for “Als Produkt migrieren.”
- **Cart/table-order/recovery:** Include of Modifiers and mapping to `SelectedModifiers` so legacy rows still display and total correctly.
- **Receipt:** Reading `PaymentItem.Modifiers` from payment JSON and emitting main + modifier lines for existing payments.
- **Request/response shape:** Optional `SelectedModifiers` on add-item/update-item (accepted, ignored for write); `SelectedModifiers` on cart and recovery responses; `PaymentItemRequest.modifierIds?` in types. POS does not set these on write.
- **DB and entities:** Tables and models for CartItemModifier, TableOrderItemModifier, ProductModifier, PaymentItem.Modifiers (in JSON) for historical data and migration.

---

## 7. Historical compatibility only

The following exist **only** to support existing data and migration; they are not used by the active POS write path:

- **Read:** Cart and table-order APIs loading `Modifiers` and returning `SelectedModifiers`; receipt and ReceiptService reading `PaymentItem.Modifiers`; TableOrderService reading cart Modifiers for totals.
- **Display:** POS CartContext/CartItemRow/CartDisplay/ReceiptSummary rendering `item.modifiers` when API returns them; recovery types and UI for `selectedModifiers`.
- **Request compat:** Backend accepting (and ignoring) `SelectedModifiers` on add-item/update-item; payment accepting (and not persisting) modifier payload.
- **Admin:** GET /api/modifier-groups with full Modifiers; modifier-groups page and ExtraZutatenSection legacy block; migrateLegacyModifier.

Removing these would break display/totals for legacy carts and table orders, reprint of old receipts, or admin migration. They are kept for retention and migration until business/legal allows cleanup.

---

## 8. Phase status

| Phase | Status | Notes |
|-------|--------|--------|
| **Phase A** | Done | POS payment: no modifierIds; POS-only cleanup. |
| **Phase B** | Done | selectedModifiers removed from add-item write; add-on = separate lines. |
| **Phase C** | Done | POS uses group.products only; legacy group.modifiers removed from runtime. |
| **Phase D** | Done | PR-A: payment payload. PR-B: add-item. PR-C: POS modifier-group contract (products-only). PR-D: admin product modifier-groups endpoint products-only; migration and legacy display unchanged. |
| **Phase E** | Not planned | Optional request/display cleanup (e.g. drop SelectedModifiers from request DTOs, remove ExtraZutatenSection legacy block) only if business/legal retention allows; historical read paths remain until then. |

Refactor epic is **complete**. Historical compatibility and admin migration are documented and left in place by design.
