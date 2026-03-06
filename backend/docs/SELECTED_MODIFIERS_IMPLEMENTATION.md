# Selected Modifiers – Backend Implementation Summary

## Overview

Full backend support for persisting and returning selected modifiers on cart and table-order items. Modifier prices are derived from DB only (no FE trust). Totals include modifier prices; decimal-safe and fiscal-safe.

---

## 1. Entities and DTOs

### New entities

| Entity | Table | Purpose |
|--------|--------|--------|
| **CartItemModifier** | `cart_item_modifiers` | Persisted modifier per cart line: Id, CartItemId, ModifierId, Name, Price, ModifierGroupId? |
| **TableOrderItemModifier** | `table_order_item_modifiers` | Same shape for table order lines (recovery / future sync) |

### Updated entities

- **CartItem**: added `Modifiers` collection (`ICollection<CartItemModifier>`). `UnitPrice` stores **effective unit price** (product price + sum of selected modifier prices).
- **TableOrderItem**: added `Modifiers` collection (`ICollection<TableOrderItemModifier>`).

### DTOs (existing + new)

- **SelectedModifierDto** (ModifierDTOs.cs): `Id`, `Name`, `Price` – response only, JSON camelCase.
- **SelectedModifierInputDto** (ModifierDTOs.cs): `Id`, `Name?`, `Price?`, `GroupId?` – add-item request; price ignored, derived from DB.
- **AddItemToCartRequest**: added `List<SelectedModifierInputDto>? SelectedModifiers`.
- **CartItemResponse**: already had `SelectedModifiers`; now populated from `CartItem.Modifiers`.
- **TableOrderItemInfo**: already had `SelectedModifiers`; now populated from item modifiers (cart or table order).

---

## 2. Migration Summary

**Migration:** `20260306152306_AddCartItemAndTableOrderItemModifiers`

- **cart_item_modifiers**: id (PK), cart_item_id (FK → cart_items, cascade delete), modifier_id, name, price (decimal 18,2), modifier_group_id (nullable). Index on cart_item_id.
- **table_order_item_modifiers**: id (PK), table_order_item_id (FK → table_order_items, cascade delete), modifier_id, name, price (decimal 18,2), modifier_group_id (nullable). Index on table_order_item_id.

Apply: `dotnet ef database update` (or your usual migration process).

---

## 3. add-item Request / Response

### Request (POST /api/cart/add-item)

```json
{
  "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "quantity": 2,
  "tableNumber": 1,
  "selectedModifiers": [
    { "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" },
    { "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901" }
  ]
}
```

- **productId**, **quantity** required.
- **selectedModifiers** optional. Only **id** is used; name/price/groupId are optional and price is never trusted – derived from DB.
- If any modifier id is not allowed for the product → **400 Bad Request** with `message` and `invalidModifierIds`.

### Response (200)

```json
{
  "message": "Item added to cart successfully",
  "cart": {
    "cartId": "...",
    "tableNumber": 1,
    "items": [
      {
        "id": "...",
        "productId": "...",
        "productName": "Döner",
        "quantity": 2,
        "unitPrice": 9.40,
        "totalPrice": 18.80,
        "lineNet": 15.67,
        "lineTax": 3.13,
        "taxType": 1,
        "taxRate": 0.20,
        "selectedModifiers": [
          { "id": "a1b2c3d4-...", "name": "Ketchup", "price": 0.50 },
          { "id": "b2c3d4e5-...", "name": "Mayo", "price": 0.50 }
        ]
      }
    ],
    "subtotalGross": 18.80,
    "subtotalNet": 15.67,
    "includedTaxTotal": 3.13,
    "grandTotalGross": 18.80,
    "taxSummary": [ ... ]
  }
}
```

- **unitPrice** / **totalPrice** / **lineNet** / **lineTax** and cart **totals** include modifier prices.
- **selectedModifiers** are the persisted modifiers for that line (id, name, price from DB).

---

## 4. table-orders-recovery Item with selectedModifiers

Recovery returns the same shape for both TableOrder-based and Cart-based orders.

### Example item in table-orders-recovery

```json
{
  "tableNumber": 1,
  "cartId": "...",
  "items": [
    {
      "productId": "...",
      "productName": "Döner",
      "quantity": 2,
      "price": 9.40,
      "total": 18.80,
      "unitPrice": 9.40,
      "totalPrice": 18.80,
      "taxRate": 0.20,
      "taxType": 1,
      "notes": null,
      "selectedModifiers": [
        { "id": "a1b2c3d4-...", "name": "Ketchup", "price": 0.50 },
        { "id": "b2c3d4e5-...", "name": "Mayo", "price": 0.50 }
        ]
    }
  ],
  "grandTotalGross": 18.80,
  ...
}
```

- **Cart-based recovery**: items come from `CartItem`; `SelectedModifiers` from `CartItem.Modifiers` (loaded via `Include(c => c.Items).ThenInclude(ci => ci.Modifiers)`).
- **TableOrder-based recovery**: items come from `TableOrderItem`; `SelectedModifiers` from `TableOrderItem.Modifiers` (loaded via `ThenInclude(toi => toi.Modifiers)`). TableOrderItemModifier rows are for future flows (e.g. sync from cart to table order).

---

## 5. Behaviour Summary

| Requirement | Implementation |
|-------------|----------------|
| Persist selected modifiers | CartItemModifier / TableOrderItemModifier; CartItem.UnitPrice = product + sum(modifier prices). |
| add-item accepts selectedModifiers | AddItemToCartRequest.SelectedModifiers; validate via IProductModifierValidationService; 400 if invalid. |
| Price from DB only | GetAllowedModifiersWithPricesForProductAsync; effectiveUnitPrice = product.Price + sum(validatedModifiers.Price). |
| Line/cart totals include modifiers | UnitPrice stored with modifiers; CartMoneyHelper.ComputeLine(ci.UnitPrice, ci.Quantity, taxType); no float, decimal only. |
| BuildCartResponse selectedModifiers | Map ci.Modifiers → SelectedModifierDto list. |
| GET /cart/current | Include Items.ThenInclude(Modifiers); same BuildCartResponse. |
| table-orders-recovery | Load cart/table order with item modifiers; map to TableOrderItemInfo.SelectedModifiers. |
| Invalid product–modifier | 400 Bad Request, message + invalidModifierIds. |
| Fiscal / RKSV | CartMoneyHelper used for all line/totals; modifier prices from DB only. |

---

## 6. Files Touched

| Area | Files |
|------|--------|
| Entities | Models/CartItemModifier.cs (new), Models/TableOrderItemModifier.cs (new), Models/CartItem.cs, Models/TableOrder.cs |
| DTOs | DTOs/ModifierDTOs.cs (SelectedModifierInputDto) |
| EF | Data/AppDbContext.cs (DbSets + CartItemModifier + TableOrderItemModifier config) |
| Controller | Controllers/CartController.cs (add-item logic, request DTO, BuildCartResponse, GET current Include, recovery mapping) |
| TableOrder sync | Services/TableOrderService.cs (ConvertCartToTableOrderAsync / UpdateExistingTableOrderAsync copy CartItem.Modifiers → TableOrderItemModifier) |
| Migration | Migrations/20260306152306_AddCartItemAndTableOrderItemModifiers.cs |

---

## 7. After Implementation

- Run migration: `dotnet ef database update`.
- FE already sends `selectedModifiers` and reads `item.selectedModifiers` / `item.SelectedModifiers`; refresh recovery and cart responses will now return persisted modifiers and correct totals.
