# Phase E: Legacy Modifier Dependencies Audit

**Goal:** Audit all remaining historical legacy modifier dependencies before final cleanup. No code changes in this document—analysis only.

**Scope:** Receipt generation, payment historical snapshot, recovery/table-order historical compatibility, CartItem.modifiers rendering, and endpoints/DTOs needed only for historical data readability.

---

## 1. Receipt generation

| Location | What uses legacy modifiers | Purpose |
|----------|----------------------------|----------|
| **PaymentService.GetReceiptDataAsync** | Deserializes `payment.PaymentItems` (JSON). For each `PaymentItem`, if `item.Modifiers` has entries: emits one main line (base only) + nested modifier lines (`"+ " + m.Name`). | Display/print receipt for **existing** payments that were created with embedded modifier snapshots. |
| **ReceiptService.CreateReceiptFromPaymentAsync** | Same: reads `PaymentItem.Modifiers`, creates `ReceiptItem` rows (main + nested modifier lines) and tax line inputs. | RKSV receipt entity creation from payment; historical payments keep parent+child display. |

**Data flow:** Payment JSON in `payment_details.PaymentItems` can contain `PaymentItem.Modifiers` (list of `PaymentItemModifierSnapshot`). New payments (Phase D+) are flat (one item per product; add-ons as separate items). Old payments may still have embedded Modifiers. Receipt code supports both: flat → one line per item; legacy → main line + modifier sub-lines.

**Historical-only:** Yes. New write path never populates `PaymentItem.Modifiers`. Read path is required so that reprinting or viewing old receipts shows the same line structure as when the payment was created.

---

## 2. Payment historical snapshot handling

| Location | What uses legacy modifiers | Purpose |
|----------|----------------------------|----------|
| **PaymentItem** (model) | `List<PaymentItemModifierSnapshot> Modifiers` | Stored only inside `payment_details.PaymentItems` JSON. Not a separate table. |
| **PaymentService.CreatePaymentAsync** | Accepts `itemRequest.Modifiers` / `ModifierIds` but **does not write** them (`requestedModifierIds` intentionally empty). Logs when legacy payload is sent. | Backward compat: request shape accepted; write path ignores. |
| **PaymentService** (receipt branch) | Reads `item.Modifiers` when building receipt items (see §1). | Historical receipt display. |

**Historical-only:** Yes. New payments are built from flat cart (one PaymentItem per cart line); no modifier snapshot is written. Existing payment rows in DB may still contain JSON with `Modifiers`; that JSON must remain readable for receipt and any future audit/history views.

---

## 3. Recovery / table-order historical compatibility

| Location | What uses legacy modifiers | Purpose |
|----------|----------------------------|----------|
| **CartController.GetTableOrdersForRecovery** | Loads `TableOrders` with `.Include(to => to.Items).ThenInclude(toi => toi.Modifiers)` and `Carts` with `.Include(c => c.Items).ThenInclude(ci => ci.Modifiers)`. Maps `item.Modifiers` → `SelectedModifiers` on each `TableOrderItemInfo`. Line totals include modifier amounts. | F5 recovery: show active table orders and cart-based orders. Legacy TableOrders/rows may have `TableOrderItemModifier` rows; legacy Carts may have `CartItemModifier` rows. |
| **TableOrderService.ConvertCartToTableOrderAsync** | Loads cart with `.ThenInclude(i => i.Modifiers)`. Uses `ci.Modifiers` only for **line total calculation** (CartMoneyHelper). Does **not** write `TableOrderItemModifier` rows (Phase 3 prep). | When converting cart→table order, totals still correct for legacy carts that have CartItemModifiers; no new legacy rows created. |
| **TableOrderService.UpdateExistingTableOrderAsync** | Same: reads `ci.Modifiers` for totals; does not write TableOrderItemModifiers. | Same as above for update path. |
| **TableOrderItem.Modifiers** (model) | `ICollection<TableOrderItemModifier>`. Table `table_order_item_modifiers`. | Legacy rows may exist from pre–Phase 3. Recovery must load and serialize to `SelectedModifiers` so UI shows correct line items and amounts. |
| **CartItem.Modifiers** (model) | `ICollection<CartItemModifier>`. Table `cart_item_modifiers`. | Legacy rows may exist. GetCart and recovery load them and map to `SelectedModifiers`. |

**Historical-only:** Partially. **Read** path is historical (old data). **Write** path for both CartItemModifier and TableOrderItemModifier is already disabled (Phase 2/3). Removal of the **read** path (Include + mapping to SelectedModifiers) would break display and totals for any cart or table order that still has modifier rows in the DB.

---

## 4. Remaining CartItem.modifiers rendering paths (POS frontend)

| Location | What uses modifiers | Purpose |
|----------|---------------------|----------|
| **CartContext** (sync from API) | Maps backend `SelectedModifiers` / `selectedModifiers` / `Modifiers` / `modifiers` → `item.modifiers`. Used for line total, merge key, and display. | When GetCart or recovery returns items with SelectedModifiers (legacy carts/table orders), POS state has `item.modifiers` so totals and UI are correct. |
| **CartContext** (addItem) | Does **not** send `selectedModifiers` (Phase D PR-B). Options.modifiers still accepted for type shape; not sent. | Add path is already modernized. |
| **CartContext** (updateItem, addModifier, incrementModifier, decrementModifier, removeModifier) | Reads/writes `item.modifiers` and in updateItem still sends `SelectedModifiers` in request body (backend ignores for write). | Legacy: allows editing quantity/removal of modifiers on existing legacy lines. Backend ignores write; display and totals still need read. |
| **cash-register.tsx** | `selectedModifiersForProduct`: derives from cart items' `item.modifiers` (productId → modifiers) for modifier sheet. | Used for “pending” display per product; legacy lines contribute. |
| **CartItemRow** | Renders `item.modifiers` (names, prices, quantity, +/-/remove). | Legacy cart lines with modifiers show as parent + modifier sub-lines. |
| **CartDisplay** | Uses `item.modifiers` in merge key (productId + modifier ids). | Deduplication of legacy lines. |
| **ReceiptSummary** | Renders `item.modifiers` (sub-lines under product). | Receipt preview may show legacy structure. |
| **_layout.tsx** | Passes `item.modifiers` into payment payload (modifierId, name, priceDelta). Backend ignores for payment write. | Legacy shape in payload; backend does not persist modifier snapshot. |
| **useTableOrdersRecoveryOptimized** | Type includes `selectedModifiers` on items. | Recovery response shape; backend sends it for legacy items. |

**Historical-only:** Display and state handling are only needed when backend actually returns `SelectedModifiers` (i.e. when cart or table order has CartItemModifier/TableOrderItemModifier rows). New flows produce flat carts; no new modifier rows. So **rendering** of `item.modifiers` is for historical (or rare legacy) data only.

---

## 5. Endpoints and DTOs still needed only for historical data readability

| Endpoint / DTO | Legacy modifier usage | Needed for |
|----------------|-----------------------|------------|
| **GET /api/cart**, **GET /api/cart/{cartId}** | Cart loaded with `.ThenInclude(i => i.Modifiers)`. `BuildCartResponse` maps `ci.Modifiers` → `CartItemResponse.SelectedModifiers`. | Cart display and recovery when cart has CartItemModifiers. |
| **GET /api/cart/table-orders-recovery** | TableOrders and Carts with Modifiers included; response items include `SelectedModifiers`. | Recovery UI and correct totals for legacy table orders/carts. |
| **CartItemResponse.SelectedModifiers** | Output DTO field. | API contract for cart and recovery; frontend maps to `item.modifiers`. |
| **TableOrderItemInfo.SelectedModifiers** | Output DTO field. | Same for table order recovery. |
| **AddItemToCartRequest.SelectedModifiers** | Input; accepted, ignored for write (Phase 3). | Backward compat; can be removed in Phase E if we drop request compat. |
| **UpdateCartItemRequest.SelectedModifiers** | Input; ignored for write. | Same. |
| **PaymentItem.Modifiers**, **PaymentItemModifierSnapshot** | Stored in payment JSON; read in receipt and ReceiptService. | Historical payments and receipt generation. |
| **GET /api/modifier-groups**, **GET /api/modifier-groups/{id}** | Return `ModifierGroupDto.Modifiers` (full list). | Admin: migration UI (“Als Produkt migrieren”) and legacy modifier list in ExtraZutatenSection. Not “historical payment” but “legacy config + migration”. |
| **ModifierDto**, **ModifierGroupDto.Modifiers** | DTOs. | Admin and receipt/display; ModifierDto also in SelectedModifierDto shape. |
| **SelectedModifierDto**, **SelectedModifierInputDto** | Cart/table-order line modifier shape. | Response/request; Phase 2 deprecated, read-only in response. |

**Distinction:**  
- **Cart/table-order/recovery/payment receipt:** Legacy **data** (rows or JSON) that may still exist; endpoints and DTOs must continue to **read** and expose them for display and totals.  
- **GET /api/modifier-groups (full Modifiers):** Legacy **config** and **migration**; needed until migration is complete and admin drops modifier list/migrate flow.

---

## 6. All remaining historical-only dependencies (summary)

| Area | Dependency | Type |
|------|------------|------|
| **Receipt** | PaymentService.GetReceiptDataAsync: read PaymentItem.Modifiers, emit main + modifier lines | Read-only (historical payments) |
| **Receipt** | ReceiptService.CreateReceiptFromPaymentAsync: same from payment JSON | Read-only (historical payments) |
| **Payment** | PaymentItem.Modifiers in JSON; CreatePayment does not write, only reads for receipt | Read-only (historical payment rows) |
| **Recovery** | GetTableOrdersForRecovery: Include Modifiers (cart + table order), map to SelectedModifiers | Read-only (legacy carts/table orders) |
| **Recovery** | TableOrderService: load cart with Modifiers for totals; no write of TableOrderItemModifier | Read-only |
| **Cart API** | GetCart / GetCurrentUserCart: Include Modifiers, BuildCartResponse → SelectedModifiers | Read-only (legacy carts) |
| **Cart API** | AddItemToCartRequest.SelectedModifiers, UpdateCartItemRequest.SelectedModifiers | Input accepted, ignored (can remove in E) |
| **POS UI** | CartContext: map backend SelectedModifiers → item.modifiers; totals and merge key | Display for legacy data |
| **POS UI** | CartItemRow, CartDisplay, ReceiptSummary, cash-register selectedModifiersForProduct | Display for legacy lines |
| **Admin** | GET /api/modifier-groups (+ Modifiers), modifier-groups page, ExtraZutatenSection | Migration + legacy config display |

---

## 7. Risk level for removal

| Dependency | If removed | Risk level |
|------------|------------|------------|
| Receipt: PaymentItem.Modifiers read + nested lines | Old receipts reprint/wrong or missing lines; audit mismatch | **High** (fiscal/audit) |
| Payment: PaymentItem.Modifiers in JSON (schema) | Breaking receipt generation for existing payments | **High** |
| Recovery: Include Modifiers + SelectedModifiers in response | Recovery shows wrong totals or missing lines for legacy orders | **High** (user-visible) |
| Cart GET: Include Modifiers + SelectedModifiers | Cart display wrong for legacy carts; totals wrong | **High** |
| TableOrderService: reading cart Modifiers for totals | Totals wrong when converting legacy cart to table order | **Medium** |
| POS: CartContext/CartItemRow/ReceiptSummary rendering item.modifiers | Legacy lines not shown or totals wrong in UI | **Medium** (only if backend still sends SelectedModifiers) |
| AddItemToCartRequest.SelectedModifiers, UpdateCartItemRequest.SelectedModifiers | Breaking old clients that still send them; otherwise safe to stop accepting | **Low** (if no such clients) |
| Admin: GET /api/modifier-groups Modifiers, migration UI | No migration path; legacy modifier list gone | **High** until migration done, then **Low** |

---

## 8. Prerequisites before Phase E

1. **Confirm no new legacy data:** Ensure no code path writes `CartItemModifier`, `TableOrderItemModifier`, or `PaymentItem.Modifiers` (already the case; observability logs can confirm no recent hits).
2. **Migration status:** Decide whether all legacy modifiers are migrated to products. If not, keep admin modifier list and migrate flow (GET /api/modifier-groups with Modifiers) until migration is complete.
3. **Data retention:** Decide how long historical carts/table orders/payments with modifier data must remain readable (e.g. 7 years for fiscal). Phase E “removal” may mean only stopping **write** and **request** compat; **read** path for existing DB/JSON may stay indefinitely.
4. **Observability:** Use existing Phase2.LegacyModifier.* logs to see if any receipt/recovery/cart load still sees modifiers. If zero for a long period, risk of breaking “live” legacy data is lower.
5. **Tests:** Keep Phase2ReceiptFlatTests, Phase2TableOrderRecoveryTests, Phase2CartFlatAddOnTests; add/run any Phase E tests that assert “legacy read path still works, write path does not create modifiers”.

---

## 9. Recommended final cleanup order

1. **Optional, low risk:** Remove **request** body compat: drop `SelectedModifiers` from `AddItemToCartRequest` and `UpdateCartItemRequest` (and from frontend cartService types and any code that still sets them). Backend already ignores; removal avoids confusion and allows future DTO cleanup.
2. **Optional, UI-only:** Remove or hide “Modifier (Legacy, nur Leseansicht)” in ExtraZutatenSection (product form); keep migration only on modifier-groups page. Reduces reliance on group.modifiers in product form.
3. **After migration complete:** Remove `Modifiers` from GET /api/modifier-groups (and GetById). Admin: remove modifier list and “Als Produkt migrieren” from modifier-groups page; remove ExtraZutatenSection legacy subsection. Then deprecate/remove `ModifierDto` and `ModifierGroupDto.Modifiers` from that flow (admin types updated).
4. **Do not remove (keep for historical readability):**  
   - Payment JSON schema: `PaymentItem.Modifiers` / `PaymentItemModifierSnapshot`.  
   - Receipt generation: reading `item.Modifiers` and emitting main + modifier lines.  
   - Cart GET and table-orders-recovery: Include Modifiers, map to SelectedModifiers.  
   - TableOrderService: reading cart Modifiers for line totals when converting/updating.  
   - POS: mapping backend SelectedModifiers → item.modifiers and rendering in CartItemRow/ReceiptSummary/CartDisplay (as long as backend can still return them).  
   - DB tables and entities: `CartItemModifier`, `TableOrderItemModifier`, `ProductModifier` (and ProductModifierGroup.Modifiers) for as long as historical data or migration needs them.
5. **Documentation:** After any removal, update PHASE_D_PR_* and this audit to state what was removed and what remains “read-only for historical data”.

---

## 10. Summary table: what to remove vs keep

| Item | Phase E action |
|------|----------------|
| AddItemToCartRequest.SelectedModifiers | Optional: remove (backend ignores). |
| UpdateCartItemRequest.SelectedModifiers | Optional: remove (backend ignores). |
| GET /api/modifier-groups returning Modifiers | Keep until migration done; then remove and update admin. |
| Receipt + Payment reading PaymentItem.Modifiers | **Keep** (historical receipts). |
| Cart GET + recovery including Modifiers, SelectedModifiers in response | **Keep** (historical carts/table orders). |
| POS rendering item.modifiers | **Keep** (as long as API returns SelectedModifiers). |
| CartItemModifier / TableOrderItemModifier / ProductModifier DB | **Keep** (historical data + migration). |
