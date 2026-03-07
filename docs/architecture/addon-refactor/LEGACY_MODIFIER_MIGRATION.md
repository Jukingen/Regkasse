# Legacy Modifier Migration

Technical guide for migrating legacy `ProductModifier` records to sellable add-on products (`Product` + `AddOnGroupProduct`). Legacy data is **not** deleted; migration is idempotent and operator-controlled.

---

## 1. Why migrate

- **Single catalog:** Add-ons become normal products with `IsSellableAddOn = true`, so POS and Admin use one model (products in groups) instead of two (products vs modifiers).
- **Flat cart/payment/receipt:** Migrated add-ons are added as separate product lines; no new legacy modifier rows.
- **Safe and repeatable:** Migration does not delete or alter legacy modifier rows. Already-migrated modifiers are skipped (idempotency via `Product.LegacyModifierId`).

---

## 2. Data mapping

| Legacy (ProductModifier) | New (Product + AddOnGroupProduct) |
|--------------------------|-----------------------------------|
| Name                     | Product.Name                      |
| Price                    | Product.Price (rounded 2 decimals)|
| TaxType                  | Product.TaxType, TaxRate          |
| SortOrder                | AddOnGroupProduct.SortOrder       |
| ModifierGroupId          | AddOnGroupProduct.ModifierGroupId |
| Id                       | Product.LegacyModifierId (idempotency) |
| —                        | Product.IsSellableAddOn = true    |
| —                        | Product.CategoryId = default category |

- **Barcode:** `ADDON-{productId:N}[0..12]` (unique per product).
- **Category:** Caller must pass a valid `DefaultCategoryId` (e.g. “Zusatzprodukte”). Migration fails with a clear error if the category is missing.

---

## 3. Idempotency and conflict handling

- **Already migrated:** If a product exists with `LegacyModifierId == modifier.Id`, that modifier is **skipped** (reported in `Skipped`). Re-running the migration does not create duplicate products.
- **Duplicate names:** Allowed. Each modifier becomes one product; same name in the same group yields multiple products.
- **Inactive group:** Modifier is reported in `Errors` with reason “Modifier group not found or inactive.” No product/link created.
- **Category missing:** One global error; no modifiers are processed.

---

## 4. Safety model

- **Explicit:** Migration runs only when invoked via API or CLI; it is not automatic.
- **No deletion:** Legacy modifier records are not deleted or altered.
- **Dry-run:** Use `dryRun: true` (API) or `--dryrun` (CLI) to get a report with no DB writes. Use in staging first.
- **Logging:** Migrated, skipped, and error counts (and details) are logged and returned in the response.

---

## 5. Prerequisites

1. **Database:** Apply the migration that adds `products.legacy_modifier_id` (nullable UUID, FK to `product_modifiers.id`):

   ```bash
   cd backend
   dotnet ef database update
   ```

2. **Category:** Create a category for add-on products (e.g. “Zusatzprodukte”) and note its ID for the migration call.

---

## 6. How to run

### Option A: CLI (no server)

From the backend directory:

```bash
# Dry run (no writes). Replace CATEGORY_UUID with a valid category ID.
dotnet run -- migrate-legacy-modifiers CATEGORY_UUID --dryrun

# Actual migration
dotnet run -- migrate-legacy-modifiers CATEGORY_UUID
```

- **Category:** First argument, or from config:
  - `appsettings.json`: `"Migration": { "DefaultCategoryId": "<guid>" }`
  - Environment: `MIGRATE_DEFAULT_CATEGORY_ID=<guid>`
- **Exit code:** `0` on success, `1` on missing category or migration errors.
- **Output:** Total processed, migrated, skipped, and errors (and optional details).

### Option B: Admin API

**POST** `/api/admin/migrate-legacy-modifiers`

- **Auth:** JWT; role **Administrator**.
- **Body:**

  ```json
  {
    "defaultCategoryId": "<valid-category-uuid>",
    "dryRun": false
  }
  ```

- **dryRun: true** – No DB writes; same response shape (counts and lists).

---

## 7. Response shape

```json
{
  "totalProcessed": 10,
  "migratedCount": 8,
  "skippedCount": 1,
  "errorCount": 1,
  "migrated": [
    { "modifierId": "...", "modifierName": "Ketchup", "productId": "...", "groupId": "..." }
  ],
  "skipped": [
    { "modifierId": "...", "modifierName": "Mayo", "productId": "...", "groupId": "..." }
  ],
  "errors": [
    { "modifierId": "...", "modifierName": "X", "reason": "Modifier group not found or inactive." }
  ]
}
```

---

## 8. Recommended flow

1. Create add-on category in Admin if needed.
2. **Staging:** Run with **dry run** (CLI: `--dryrun`, API: `dryRun: true`). Check report (migrated/skipped/errors).
3. Run actual migration (CLI or API with `dryRun: false`).
4. Verify in Admin: Add-on groups show new products; legacy modifiers still visible read-only.
5. **Production:** Repeat when ready; avoid running two migrations in parallel.

---

## 9. Rollback

- **Application:** Migration does not remove or change legacy modifiers. To “roll back” migrated products, delete the created products (and their `addon_group_products` links) manually; handle `legacy_modifier_id` per your cleanup policy.
- **Schema:** To remove the migration column, revert the EF migration (e.g. `dotnet ef database update <PreviousMigrationName>`).

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| Wrong or deleted category | Migration returns a clear error; no partial writes. |
| Parallel runs | Avoid; run from a single process. |
| Barcode collision | Barcode is derived from new product ID; no collision with existing products. |

---

## 11. References

- [PHASE2_IMPLEMENTATION.md](./PHASE2_IMPLEMENTATION.md) – Overall Phase 2 design and rollout.
- `ai/PHASE2_LEGACY_MODIFIER_MIGRATION.md` – Original implementation notes and examples.
