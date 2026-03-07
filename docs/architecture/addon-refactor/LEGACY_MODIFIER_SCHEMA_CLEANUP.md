# Legacy Modifier Schema Cleanup

## 1. Artifacts Related to Legacy Modifier / Migration

| Artifact | Type | Classification | Action |
|----------|------|----------------|--------|
| **product_modifiers** | Table | **Safe to drop now** | Legacy modifier definitions only. No FK from cart_item_modifiers or table_order_item_modifiers. API and runtime no longer read/write. |
| **IX_product_modifiers_modifier_group_id** | Index | **Safe to drop** | Dropped with table. |
| **FK_product_modifiers_product_modifier_groups_modifier_group_id** | FK | **Safe to drop** | Dropped with table. |
| **cart_item_modifiers** | Table | **Historical data decision** | Kept. Stores legacy cart line modifier selections (name/price denormalized). No FK to product_modifiers. Read-only for recovery/totals. |
| **table_order_item_modifiers** | Table | **Historical data decision** | Kept. Same as above for table orders. |
| **ProductModifier** | Entity | **Safe to remove** | Removed with table. |
| **ProductModifierGroup.Modifiers** | Navigation | **Safe to remove** | Removed; API returns products only. |
| **ProductModifierValidationService** | Service | **Safe to remove** | Replaced by stub (returns empty). No new payment/cart flow uses legacy modifiers. |
| Migration progress / mapping tables | — | **None** | No dedicated DB tables; progress was computed on the fly. |

## 2. Not Removed (Active Model)

- **product_modifier_groups** – Add-on groups (kept).
- **product_modifier_group_assignments** – Product ↔ group assignment (kept).
- **addon_group_products** – Add-on products in groups (kept).
- **CartItemModifier / TableOrderItemModifier** – Entities kept for historical read; tables kept.

## 3. Migration Name

`DropProductModifiersTable`

## 4. Seed / demo data

- No seed or demo data in the solution created `ProductModifier` rows. Seed/demo uses only ProductModifierGroup, ProductModifierGroupAssignment, AddOnGroupProduct, and Product (IsSellableAddOn). No updates required.

## 5. Dropped (summary)

| Item | Type |
|------|------|
| **product_modifiers** | Table |
| **IX_product_modifiers_modifier_group_id** | Index (dropped with table) |
| **FK_product_modifiers_product_modifier_groups_modifier_group_id** | FK (dropped with table) |
| **ProductModifier** | Entity (file removed) |
| **ProductModifierGroup.Modifiers** | Navigation (removed) |
| **ProductModifierValidationService** | Service (replaced by NoOpProductModifierValidationService) |

## 6. Rollback notes

- **Migration rollback:** Run `dotnet ef database update <PreviousMigrationName>` to revert. The `Down` in `20260307204756_DropProductModifiersTable.cs` recreates the `product_modifiers` table and index. Data that was in `product_modifiers` before the drop is **not** restored; restore from backup if needed.
- **Code rollback:** Restore `Models/ProductModifier.cs`, `ProductModifierGroup.Modifiers`, `AppDbContext` DbSet and entity configuration, and `ProductModifierValidationService.cs`; re-register `ProductModifierValidationService` in Program.cs.
