# Legacy Modifier – Architectural Recommendation

**Date:** 2025-03-07  
**Based on:** Actual codebase inspection (not docs)

---

## 1. Actual Remaining Dependencies on Legacy Modifiers

### 1.1 ProductModifier Table (`product_modifiers`)

| Consumer | Dependency | Required? |
|----------|------------|-----------|
| **ModifierMigrationService** | Reads `ProductModifiers` to migrate to Products | **Yes** – until migration complete |
| **ModifierGroupsController** | `Include(g => g.Modifiers)`, `MapToModifierGroupDto` → returns `group.modifiers` | **Yes** – admin migration UI + API contract |
| **ProductController** (GetCatalog, GetProductModifierGroups) | `Include(g => g.Modifiers.Where(m => m.IsActive))` → `group.modifiers` in catalog | **Yes** – POS fallback when `group.products` empty |
| **Admin modifier-groups page** | Displays legacy modifiers, "Als Produkt migrieren" | **Yes** – migration action |
| **POS ProductRow / ProductGridCard** | Fallback: `group.modifiers` when `group.products` empty | **Yes** – for groups not yet migrated |
| **ModifierSelectionBottomSheet / Modal** | `group.modifiers` when `group.products` empty | **Yes** – same fallback |

### 1.2 CartItemModifier / TableOrderItemModifier (Embedded Modifiers)

| Consumer | Dependency | ProductModifier Lookup? |
|----------|------------|--------------------------|
| **CartController.BuildCartResponse** | Maps `CartItemModifiers` → `SelectedModifiers` | **No** – uses `m.Name`, `m.Price` from row |
| **TableOrderService** (recovery) | Maps `TableOrderItemModifiers` → `SelectedModifiers` | **No** – uses denormalized Name, Price |
| **CartController** (merge logic) | `!_context.CartItemModifiers.Any(m => m.CartItemId == ci.Id)` | **No** – existence check only |

**Conclusion:** Old carts and table orders load **without** ProductModifier. CartItemModifier and TableOrderItemModifier store Name, Price denormalized.

### 1.3 PaymentItem.Modifiers (Payment Snapshots)

| Consumer | Dependency | ProductModifier Lookup? |
|----------|------------|--------------------------|
| **PaymentService** (create) | `requestedModifierIds` always empty – block unreachable | **No** – no new modifier writes |
| **PaymentService.GetReceiptDataAsync** | Reads `item.Modifiers` from PaymentDetails JSON | **No** – snapshot data in JSON |
| **Receipt rendering** | Uses `m.LineNet`, `m.TotalPrice`, `m.TaxAmount` from snapshot | **No** – no ProductModifier lookup |

**Conclusion:** Old receipts render **without** ProductModifier. PaymentItem.Modifiers are snapshots in JSON.

### 1.4 Audit / History

No code path found that reads ProductModifier for audit or history. Cart/order/payment logs use denormalized structures.

---

## 2. Risk of Removing Legacy Modifiers Too Early

| Action | Risk |
|--------|------|
| **Drop `product_modifiers` table** | ModifierMigrationService fails; admin cannot migrate; POS groups with only modifiers show no add-ons |
| **Stop returning `group.modifiers` in API** | POS fallback breaks for groups that have no products yet; admin cannot see or migrate |
| **Hide legacy modifiers from admin** | Admin cannot run "Als Produkt migrieren"; migration blocked |
| **Remove CartItemModifier / TableOrderItemModifier tables** | Old carts and table orders fail to load; BuildCartResponse and recovery break |

**Safe to remove only when:**
- All ProductModifiers migrated to Products (AddOnGroupProduct)
- No groups have `group.products` empty while `group.modifiers` non-empty
- Retention period for old carts/orders/payments has passed (or data migrated)

---

## 3. Recommended Visibility Level in Admin

**Recommendation: Option A – Keep visible read-only in admin with migration action**

**Rationale:**
- Migration must complete before any removal. Hiding blocks migration.
- Admin needs to see which modifiers exist and run "Als Produkt migrieren."
- Current UI already separates "Add-on-Produkte (empfohlen)" and "Legacy-Modifier (Kompatibilität)" with clear wording.
- Option B (hide) would block migration. Option C (remove) is not safe until migration complete and retention satisfied.

---

## 4. Phased Plan

### Phase: Now

| Action | Details |
|--------|---------|
| **Keep** | Legacy modifiers visible in admin; "Legacy-Modifier (Kompatibilität)" section |
| **Keep** | "Als Produkt migrieren" button for non-migrated modifiers |
| **Keep** | `group.modifiers` in API response (catalog, modifier-groups) |
| **Keep** | POS fallback: `group.products` primary, `group.modifiers` when products empty |
| **Keep** | CartItemModifier, TableOrderItemModifier tables (read-only for historical data) |
| **Ensure** | Migration endpoint production-safe (Description, Cost set; transaction) |
| **Ensure** | New add-ons always use Products; no new CartItemModifier/TableOrderItemModifier writes |

### Phase: After Migration Complete

**Prerequisite:** All ProductModifiers migrated; every group has `group.products` non-empty (or group has no add-ons).

| Action | Details |
|--------|---------|
| **Optional: collapse** | Legacy section in admin (collapsed by default); keep "Als Produkt migrieren" reachable for edge cases |
| **Optional: stop including** | `g.Modifiers` in API if all groups have products (reduces payload) |
| **Do not** | Drop product_modifiers table yet – historical reference; CartItemModifier.ModifierId may still reference it |
| **Verify** | No POS code path hits modifiers fallback (all groups have products) |

### Phase: Final Cleanup (Later)

**Prerequisite:** Migration complete; no carts/orders/payments with modifiers in production (or retention passed); audit/history no longer needs modifier data.

| Action | Details |
|--------|---------|
| **Consider** | Stop including `g.Modifiers` in API; remove from MapToModifierGroupDto |
| **Consider** | Drop `product_modifiers` table only if no FK from CartItemModifier/TableOrderItemModifier (they store ModifierId but may not have FK) |
| **High risk** | Dropping CartItemModifier, TableOrderItemModifier – requires data migration of historical carts/orders |
| **High risk** | Removing PaymentItem.Modifiers deserialization – historical receipts would break |

**Note:** CartItemModifier and TableOrderItemModifier have `ModifierId` column but no FK to ProductModifier in EF config. Dropping product_modifiers would orphan the ModifierId values but would not cause FK violation. Display still works (Name, Price denormalized). Drop order: product_modifiers can be dropped before CartItemModifier/TableOrderItemModifier if no FK exists.

---

## 5. Summary Table

| Area | Needs ProductModifier? | Needs CartItemModifier? | Needs TableOrderItemModifier? | Needs PaymentItem.Modifiers? |
|------|------------------------|-------------------------|-------------------------------|------------------------------|
| Old carts load | No | Yes (Name, Price) | – | – |
| Old orders load | No | – | Yes (Name, Price) | – |
| Old receipts render | No | – | – | Yes (snapshot in JSON) |
| Migration | Yes | – | – | – |
| Admin migration UI | Yes | – | – | – |
| POS fallback | Yes (via API) | – | – | – |
| New add-on flow | No | No | No | No |

---

## 6. Final Recommendation

**Option A** – Keep legacy modifiers visible read-only in admin with migration action.

**Do not** choose B (hide) or C (remove) until migration is complete and the prerequisites for final cleanup are satisfied.
