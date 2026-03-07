# Legacy Modifier Migration – Implementation Report

**Date:** 2025-03-07  
**Scope:** Fix HTTP 500 on single modifier migration; production-safety review.

---

## 1. Root Cause of HTTP 500

**Observed error:** `insert into products` fails – `description` is null.  
**DB constraint:** `products.description` is NOT NULL in production (schema may differ from EF model).

**Root cause:** Migration and AddProductToGroup created `Product` entities without setting `Description`. The C# model has `Description` as `string?` (nullable), but the production PostgreSQL schema enforces NOT NULL. EF Core inserts NULL when the property is not set, causing the insert to fail.

---

## 2. Exact Files Changed

| File | Change |
|------|--------|
| `backend/Services/ModifierMigrationService.cs` | Added `CreateAddOnProductFromModifier` helper; set `Description`, `Cost` explicitly; both batch and single migration use helper |
| `backend/Controllers/ModifierGroupsController.cs` | Set `Description = name`, `Cost = 0` when creating new add-on product via `CreateNewAddOnProduct` |
| `backend/KasseAPI_Final.Tests/ModifierMigrationServiceTests.cs` | Added `Description` to test setup Product; added `MigrateAsync_CreatedProduct_HasDescriptionNeverNull`; asserted `Description` in existing test |

---

## 3. Product Fields That Needed Explicit Defaults

| Field | Value | Reason |
|-------|-------|--------|
| `Description` | `mod.Name ?? string.Empty` (migration) / `create.Name ?? string.Empty` (new add-on) | DB NOT NULL; must never be null |
| `Cost` | `0` | Some DBs/EF configs expect non-null; explicit for safety |

**Other fields** already set: Id, Name, Price, TaxType, Category, CategoryId, StockQuantity, MinStockLevel, Unit, Barcode, IsActive, IsSellableAddOn, TaxRate, RksvProductType, CreatedAt, UpdatedAt.

---

## 4. Final Transaction / Order of Operations

### Single migration (`MigrateSingleByModifierIdAsync` → `MigrateSingleAsync`)

1. Load modifier + group; validate modifier exists, category exists, group active.
2. **Idempotency check:** If add-on product with same Name+Price already in group → return "AlreadyMigrated"; optionally mark modifier inactive.
3. **Create product** via `CreateAddOnProductFromModifier` (all required fields set).
4. **Create link** `AddOnGroupProduct`.
5. **Deactivate modifier** if `markModifierInactive`.
6. **SaveChanges** (single round-trip).

**Transaction:** When `_context.Database.IsRelational()`, the entire `MigrateSingleAsync` runs inside `BeginTransactionAsync`. On any exception, `RollbackAsync` is called. Modifier is not deactivated if product/link creation fails.

### Batch migration (`MigrateAsync`)

Per-modifier: create product + link in one `SaveChangesAsync`. No modifier deactivation in batch. Each modifier is atomic (product + link together). No full-batch transaction (partial success is intentional).

---

## 5. Legacy Modifiers – Visibility Recommendation

**Recommendation: Option A** – Keep legacy modifiers visible in admin as a small read-only compatibility section with migration actions.

**Rationale (from code inspection):**

- **Historical carts:** `BuildCartResponse` maps `CartItemModifiers` to `SelectedModifiers`; carts with modifiers must load.
- **Historical table orders:** `TableOrderItemModifiers` in DB; recovery API returns `SelectedModifiers`.
- **Historical payments:** `PaymentDetails.PaymentItems` JSON may contain `Modifiers`; receipt rendering uses them.
- **Migration source:** `ModifierMigrationService` reads `ProductModifiers`; migration must complete before removal.
- **Admin UI:** Already shows legacy modifiers under "Legacy-Modifier (Kompatibilität)" with "Als Produkt migrieren" and clear text that they are compatibility-only.

**Current admin UI:** Correct. Legacy modifiers are:
- Shown in a separate "Legacy-Modifier (Kompatibilität)" section.
- Labeled as "nur der Kompatibilität" and "Für neue Add-ons bitte Produkte verwenden."
- Migrated modifiers show "migriert" tag.
- Non-migrated have "Als Produkt migrieren" button.

---

## 6. What Should Happen Next

| Action | When |
|--------|------|
| **Keep now** | Legacy modifiers visible in admin; migration tooling; `group.products` primary; `group.modifiers` for legacy |
| **Hide/de-emphasize next** | Consider collapsing legacy section by default once most modifiers are migrated; keep "Als Produkt migrieren" accessible |
| **Remove later** | Only when: (a) all modifiers migrated, (b) no carts/orders/payments with modifiers in production, (c) audit/history no longer needs modifier data |

---

## 7. Phase 1 – Mismatch Report (Docs vs Code)

| Docs claim | Code reality | Status |
|------------|--------------|--------|
| Add-on = Product | ✅ Implemented: `IsSellableAddOn`, `AddOnGroupProduct` | Match |
| `group.products` primary | ✅ API returns both; POS prefers products | Match |
| Legacy modifiers compatibility-only | ✅ No new modifier creation; migration creates Products | Match |
| Migration tooling production-safe | ❌ Was broken: `Description` null → HTTP 500 | **Fixed** |
| Transactional single migration | ✅ `MigrateSingleByModifierIdAsync` uses transaction | Match |
| Idempotent migration | ✅ Name+Price match = skip | Match |

---

## 8. Tests Added/Updated

- `MigrateAsync_ValidCategoryAndOneModifier_CreatesProductAndLink`: Asserts `product.Description` is not null and equals modifier name.
- `MigrateAsync_CreatedProduct_HasDescriptionNeverNull`: Dedicated test for Description on migrated product.
- Test setup Product in `MigrateSingleByModifierId_InactiveModifierWithExistingProduct`: Added `Description = "Ketchup"` for DB compatibility.
