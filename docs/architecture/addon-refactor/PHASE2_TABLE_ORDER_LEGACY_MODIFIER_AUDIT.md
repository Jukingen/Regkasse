# Phase 2 – Table Order Legacy Modifier Audit and Simplification

Audit of table-order flows and alignment with the flat add-on product model. Read compatibility for historical data is preserved.

---

## 1. Current dependency map

```
Cart (Items[*].Modifiers)     →  TableOrderService.ConvertCartToTableOrderAsync
Cart (Items[*].Modifiers)     →  TableOrderService.UpdateExistingTableOrderAsync
                                        ↓
                              TableOrderItemModifier (write)
                                        ↓
TableOrder (Items[*].Modifiers)  ←  CartController.GetTableOrdersForRecovery
Cart (no TableOrder for table)   ←  CartController.GetTableOrdersForRecovery (cart-based fallback)
                                        ↓
                              TableOrderRecoveryResponse.Items[*].SelectedModifiers (read)
```

| Component | Role | Legacy modifier usage |
|-----------|------|------------------------|
| **TableOrderService.ConvertCartToTableOrderAsync** | Cart → TableOrder persist | Copies `CartItem.Modifiers` → `TableOrderItemModifier` for each cart item that has modifiers. |
| **TableOrderService.UpdateExistingTableOrderAsync** | Refresh TableOrder from Cart | Same: after replacing items, copies `CartItem.Modifiers` → `TableOrderItemModifier`. |
| **TableOrderService.MigrateActiveCartsToTableOrdersAsync** | Background migration | Calls `ConvertCartToTableOrderAsync`; inherits same copy behavior. |
| **CartController.GetTableOrdersForRecovery** | F5 recovery API | Reads `TableOrder.Items[*].Modifiers` → `TableOrderItemInfo.SelectedModifiers`; for cart-only path reads `CartItem.Modifiers` → `SelectedModifiers`. |
| **TableOrder** / **TableOrderItem** | EF models | `TableOrderItem.Modifiers` navigation to `TableOrderItemModifier`. |
| **TableOrderItemModifier** | EF model | Legacy entity; one row per selected modifier on a table order line. |
| **TableOrderItemInfo** (DTO) | Recovery response | `SelectedModifiers` (obsolete, read-only) populated from `TableOrderItem.Modifiers` or `CartItem.Modifiers`. |

**Where CartItem.Modifiers are copied into TableOrderItemModifier**

- **TableOrderService.cs**  
  - **ConvertCartToTableOrderAsync** (after first `SaveChangesAsync`): loop over `cart.Items`, for each `cartItem.Modifiers` create `TableOrderItemModifier` and add to context.  
  - **UpdateExistingTableOrderAsync** (after replacing items): same loop for `cart.Items` → `TableOrderItemModifier`.

No other code path creates `TableOrderItemModifier`; all writes go through this service.

---

## 2. Flat product-only direction for new table-order writes

- **New flow (already in place):** Add-ons are separate `CartItem`s (no `CartItem.Modifiers`). When converting cart to table order, each `CartItem` becomes one `TableOrderItem`. So “Burger” and “Extra Cheese” (add-on product) become two `TableOrderItem`s and **no** `TableOrderItemModifier` is created.
- **Legacy flow:** Cart items that still have `CartItem.Modifiers` (e.g. old clients or old data) are copied to `TableOrderItemModifier` so recovery and display keep working.

No change to the write logic is required for flat behavior: the copy loop runs over `(cartItem.Modifiers ?? Enumerable.Empty<CartItemModifier>())`. If the cart is flat, the collection is empty and no `TableOrderItemModifier` rows are created.

**Implemented:**

- Comments in **TableOrderService** state that the new flow is flat and that `TableOrderItemModifier` is only for legacy `CartItem.Modifiers`.
- **Observability:** When any modifier is copied, the service logs:
  - `Phase2.LegacyModifier.TableOrderCreatedWithLegacyModifiers` (new table order)
  - `Phase2.LegacyModifier.TableOrderUpdatedWithLegacyModifiers` (update from cart)
- Model comments: **TableOrderItem.Modifiers** and **TableOrderItemModifier** documented as legacy; flat model uses separate items only.

---

## 3. Read compatibility for historical data

- **GetTableOrdersForRecovery** continues to:
  - Load `TableOrder` with `Items` and `Items[*].Modifiers`.
  - Map `item.Modifiers` → `TableOrderItemInfo.SelectedModifiers` for table-order source.
  - For cart-based fallback, map `CartItem.Modifiers` → `TableOrderItemInfo.SelectedModifiers`.
- **TableOrderItemInfo.SelectedModifiers** remains populated and is marked obsolete (read-only); no breaking change for clients that still use it.

---

## 4. Recommended cleanup sequence (Phase 3 or later)

1. **Measure:** Use logs `Phase2.LegacyModifier.TableOrderCreatedWithLegacyModifiers` and `TableOrderUpdatedWithLegacyModifiers`; when they no longer appear over a chosen window, no new legacy modifier copies are written.
2. **Optional:** Add a similar observability log in **GetTableOrdersForRecovery** when any table order or cart-based item has `SelectedModifiers.Count > 0` (to measure read-side legacy usage).
3. **After zero write usage:** Stop creating new `TableOrderItemModifier` in **TableOrderService** (e.g. guard the copy loop with a config or remove it once legacy carts are migrated).
4. **After zero read usage:** Remove or simplify `SelectedModifiers` from **TableOrderItemInfo** and stop loading `TableOrderItem.Modifiers` / `CartItem.Modifiers` in recovery where no longer needed.
5. **Schema:** Only after no reliance on `TableOrderItemModifier` (and no need to show historical modifier lines), consider deprecating or dropping the `table_order_item_modifiers` table and the entity.

---

## 5. Exact files changed (Step 12)

| File | Change |
|------|--------|
| **backend/Services/TableOrderService.cs** | Added `Microsoft.Extensions.Logging` using; class summary updated for Phase 2 flat vs legacy. In **ConvertCartToTableOrderAsync**: comment that modifier copy is legacy-only; set `Quantity = mod.Quantity` on new `TableOrderItemModifier`; count copied modifiers and log `Phase2.LegacyModifier.TableOrderCreatedWithLegacyModifiers` when count > 0. In **UpdateExistingTableOrderAsync**: same comment; count and log `Phase2.LegacyModifier.TableOrderUpdatedWithLegacyModifiers` when count > 0. |
| **backend/Models/TableOrder.cs** | **TableOrderItem.Modifiers** XML summary updated: legacy; Phase 2 flat uses separate items only. |
| **backend/Models/TableOrderItemModifier.cs** | Class XML summary updated: legacy; Phase 2 flat does not create this entity; kept for historical/backward compatibility. |
| **docs/architecture/addon-refactor/PHASE2_TABLE_ORDER_LEGACY_MODIFIER_AUDIT.md** | New: dependency map, flat direction, read compatibility, cleanup sequence, files changed. |

No changes to **CartController** (recovery), **AppDbContext**, or DTOs beyond existing obsolete/comment on **TableOrderItemInfo.SelectedModifiers**.

---

## 6. Remaining legacy dependencies

| Location | Dependency | When removable |
|----------|-------------|----------------|
| **TableOrderService** (both methods) | Loop that copies `CartItem.Modifiers` → `TableOrderItemModifier` | When no carts with `CartItem.Modifiers` are converted (observe via new logs). |
| **CartController.GetTableOrdersForRecovery** | Loads `TableOrderItem.Modifiers`, maps to `SelectedModifiers`; cart path maps `CartItem.Modifiers` to `SelectedModifiers` | When no clients need `SelectedModifiers` and no historical table orders/carts with modifiers need to be displayed. |
| **TableOrderItemInfo.SelectedModifiers** | DTO field (obsolete) | With above; keep until read path is simplified. |
| **TableOrderItem.Modifiers** nav | EF model | Keep until table/schema cleanup. |
| **TableOrderItemModifier** entity / table | DB and model | After all read/write paths no longer use it (Phase 3+). |

---

## 7. Event names for observability

- `Phase2.LegacyModifier.TableOrderCreatedWithLegacyModifiers` — TableOrderId, CartId, CopiedModifiersCount  
- `Phase2.LegacyModifier.TableOrderUpdatedWithLegacyModifiers` — TableOrderId, CartId, CopiedModifiersCount  

When these events no longer occur, new table-order writes are fully flat (no legacy modifier copies).
