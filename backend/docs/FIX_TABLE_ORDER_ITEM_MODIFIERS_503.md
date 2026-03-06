# Fix: GET /api/cart/table-orders-recovery 503 — relation "table_order_item_modifiers" does not exist

## Root cause summary

**Cause: migration not applied** — the migration that creates `cart_item_modifiers` and `table_order_item_modifiers` existed in the codebase but had **not been applied** to the PostgreSQL database.

- **Not** a wrong table name: EF and migration both use `table_order_item_modifiers` and `cart_item_modifiers` (see `AppDbContext` and `20260306152306_AddCartItemAndTableOrderItemModifiers.cs`).
- **Not** a missing migration: the migration file was present and the model snapshot was up to date.
- **Not** a copy/paste error: `TableOrderItemModifier` is mapped to `table_order_item_modifiers`; `CartItemModifier` to `cart_item_modifiers`; FKs point to `table_order_items.Id` and `cart_items.id` respectively.

After the modifier persistence change, `GetTableOrdersForRecovery()` loads:

- `TableOrders` with `.Include(to => to.Items).ThenInclude(toi => toi.Modifiers)`
- `Carts` with `.Include(c => c.Items).ThenInclude(ci => ci.Modifiers)`

So EF runs a query that joins to `table_order_item_modifiers` and `cart_item_modifiers`. If those tables are missing, PostgreSQL returns `42P01: relation "table_order_item_modifiers" does not exist` and the endpoint fails (e.g. 503).

---

## Code / config verification (no changes required)

| Item | Location | Status |
|------|----------|--------|
| Migration | `Migrations/20260306152306_AddCartItemAndTableOrderItemModifiers.cs` | Creates `cart_item_modifiers` and `table_order_item_modifiers` with correct columns and FKs. |
| CartItemModifier | `Models/CartItemModifier.cs` | `[Table("cart_item_modifiers")]`; `CartItemId` FK. |
| TableOrderItemModifier | `Models/TableOrderItemModifier.cs` | `[Table("table_order_item_modifiers")]`; `TableOrderItemId` FK. |
| AppDbContext | `Data/AppDbContext.cs` L733–744, L846–858 | Both entities configured; `.ToTable("cart_item_modifiers")` and `.ToTable("table_order_item_modifiers")`; FK to `CartItem` / `TableOrderItem` with cascade delete. |
| TableOrderItem.Modifiers | `Models/TableOrder.cs` L134 | `ICollection<TableOrderItemModifier> Modifiers`. |
| CartItem.Modifiers | `Models/CartItem.cs` L29 | `ICollection<CartItemModifier> Modifiers`. |
| FK in migration | Migration L52–55 | `table_order_item_id` → `table_order_items.Id`. |
| FK in migration | Migration L29–32 | `cart_item_id` → `cart_items.id`. |

No code or mapping fix was required; the schema was simply not applied.

---

## Fix applied

Apply the existing migration so the two tables exist in the database:

```bash
cd backend
dotnet ef database update --context AppDbContext
```

Result (when run locally):

- `Applying migration '20260306152306_AddCartItemAndTableOrderItemModifiers'.`
- Tables created: `cart_item_modifiers`, `table_order_item_modifiers`, with indexes and FKs as in the migration.

---

## Commands reference

**If the migration had been missing (it was not):**

```bash
cd backend
dotnet ef migrations add AddCartItemAndTableOrderItemModifiers --context AppDbContext
dotnet ef database update --context AppDbContext
```

**Actual fix (migration already present):**

```bash
cd backend
dotnet ef database update --context AppDbContext
```

---

## Verification

After applying the migration:

1. **GET /api/cart/table-orders-recovery** returns **200** (no 503 from missing table).
2. When cart or table-order items have persisted modifiers, the response includes `selectedModifiers` (or `SelectedModifiers`) per item, and the recovery query no longer throws on the new tables.

The existing 503 fallback (e.g. for real infra or provisioning issues) remains; this fix addresses the case where the modifier tables were missing because the migration had not been run.
