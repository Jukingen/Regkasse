# Verification: Modifier Persistence and Totals

Exact code references for the four main scenarios plus invalid-modifier handling and FE/backend total alignment.

---

## 1. POST /cart/add-item with selectedModifiers persists modifiers in DB

**Endpoint:** `CartController.AddItemToCart`  
**File:** `backend/Controllers/CartController.cs`

| Step | Location | Code reference |
|------|----------|-----------------|
| Request DTO accepts selected modifiers | `CartController.cs` L1501–L1510 | `AddItemToCartRequest.SelectedModifiers` (type `List<SelectedModifierInputDto>?`) |
| Modifier IDs taken from request | L285–L286 | `requestedModifierIds = (request.SelectedModifiers ?? new List<SelectedModifierInputDto>()).Select(s => s.Id).Distinct().ToList()` |
| Validation and DB price lookup | L288–L298 | `_modifierValidation.GetAllowedModifiersWithPricesForProductAsync(request.ProductId, requestedModifierIds)`; invalid IDs → 400 |
| Effective unit price (product + modifiers) | L300–L301 | `effectiveUnitPrice = product.Price + validatedModifiers.Sum(m => m.Price)`; `Math.Round(..., 2, MidpointRounding.AwayFromZero)` |
| New cart item with effective unit price | L327–L336 | `cartItem = new CartItem { ..., UnitPrice = effectiveUnitPrice, ... }`; `_context.CartItems.Add(cartItem)` |
| Persist modifier rows | L337–L347 | `foreach (var mod in validatedModifiers) { _context.CartItemModifiers.Add(new CartItemModifier { CartItemId = cartItem.Id, ModifierId = mod.Id, Name = mod.Name, Price = mod.Price, ModifierGroupId = null }); }` |
| Save to DB | L354 | `await _context.SaveChangesAsync()` |

**Entity:** `CartItemModifier` – `backend/Models/CartItemModifier.cs` (table `cart_item_modifiers`, FK to `cart_items` with cascade delete).  
**EF config:** `backend/Data/AppDbContext.cs` – `CartItemModifier` entity configuration and `CartItem.Modifiers` relationship.

---

## 2. add-item response totals include modifier prices

**Same action:** `AddItemToCart`; response is built via `BuildCartResponse`.

| Step | Location | Code reference |
|------|----------|-----------------|
| Cart reloaded with modifiers | `CartController.cs` L356–L360 | `updatedCart = await _context.Carts.Include(c => c.Items).ThenInclude(i => i.Modifiers).FirstOrDefaultAsync(...)` |
| Line amounts use stored unit price (already includes modifiers) | L1425–L1461 | `BuildCartResponse`: `CartMoneyHelper.ComputeLine(ci.UnitPrice, ci.Quantity, taxType)` (L1433, L1457); `ci.UnitPrice` is the effective unit price from add-item |
| Cart totals from line amounts | L1461–L1489 | `lineAmounts` from each `ci.UnitPrice`; `totals = CartMoneyHelper.ComputeCartTotals(lineAmounts)`; response `SubtotalGross`, `SubtotalNet`, `IncludedTaxTotal`, `GrandTotalGross` from `totals` (L1484–1487) |
| Per-item TotalPrice / LineNet / LineTax | L1431–L1450 | `line = ComputeLine(ci.UnitPrice, ...)`; `TotalPrice = line.LineGross`, `LineNet`, `LineTax` set on `CartItemResponse` |

**Money helper:** `backend/Services/CartMoneyHelper.cs` – `ComputeLine(decimal unitGross, int quantity, int taxType)` (L53–60): `lineGross = Round(unitGross * quantity)`; `ComputeCartTotals` (L146–169) sums `LineGross`/`LineNet`/`LineTax` from all lines. So any modifier contribution is already in `ci.UnitPrice`, and thus in response totals.

---

## 3. GET /cart/current returns selectedModifiers

**Endpoint:** `CartController.GetCurrentUserCart`  
**File:** `backend/Controllers/CartController.cs`

| Step | Location | Code reference |
|------|----------|-----------------|
| Load cart with item modifiers | L46–L54 | `cart = await _context.Carts.Include(c => c.Items).ThenInclude(i => i.Modifiers).Include(c => c.Customer).Where(...).FirstOrDefaultAsync()` |
| Build response with selected modifiers | L134 | `cartResponse = BuildCartResponse(cart, products)` |
| Map item modifiers to DTO | L1432–L1449 | In `BuildCartResponse`: `selectedModifiers = (ci.Modifiers ?? Enumerable.Empty<CartItemModifier>()).Select(m => new SelectedModifierDto { Id = m.ModifierId, Name = m.Name, Price = m.Price }).ToList()`; assigned to `CartItemResponse.SelectedModifiers` |

So GET current returns the same `BuildCartResponse` shape as add-item, including `SelectedModifiers` per item from persisted `CartItem.Modifiers`.

---

## 4. GET /cart/table-orders-recovery restores selectedModifiers after refresh

**Endpoint:** `CartController.GetTableOrdersForRecovery`  
**File:** `backend/Controllers/CartController.cs`

### TableOrder path

| Step | Location | Code reference |
|------|----------|-----------------|
| Load table orders with item modifiers | L1121–L1131 | `userActiveTableOrders = await _context.TableOrders.Include(to => to.Items).ThenInclude(toi => toi.Modifiers).Include(...).Where(...).ToListAsync()` |
| Map each item’s modifiers to DTO | L1173–L1186 | `Items = toItems.Zip(lineAmounts, (item, line) => new TableOrderItemInfo { ..., SelectedModifiers = (item.Modifiers ?? Enumerable.Empty<TableOrderItemModifier>()).Select(m => new SelectedModifierDto { Id = m.ModifierId, Name = m.Name, Price = m.Price }).ToList() }).ToList()` |

### Cart path (no TableOrder for that table)

| Step | Location | Code reference |
|------|----------|-----------------|
| Load carts with item modifiers | L1134–L1141 | `userActiveCarts = await _context.Carts.Include(c => c.Items).ThenInclude(ci => ci.Modifiers).Include(...).Where(...).ToListAsync()` |
| Map each item’s modifiers to DTO | L1237–L1255 | `Items = items.Zip(lineAmounts, (item, line) => { return new TableOrderItemInfo { ..., SelectedModifiers = (item.Modifiers ?? Enumerable.Empty<CartItemModifier>()).Select(m => new SelectedModifierDto { Id = m.ModifierId, Name = m.Name, Price = m.Price }).ToList() }; }).ToList()` |

**TableOrderItem modifiers:** Persisted when cart is converted to table order in `TableOrderService.ConvertCartToTableOrderAsync` / `UpdateExistingTableOrderAsync` (`backend/Services/TableOrderService.cs`), which copy `CartItem.Modifiers` into `TableOrderItemModifier` rows.

**FE consumption:** `frontend/hooks/useTableOrdersRecoveryOptimized.ts` – `TableOrderRecoveryItem` has `selectedModifiers?: Array<{ id: string; name: string; price: number }>`. Cart/recovery hydration in `CartContext` uses `item.SelectedModifiers ?? item.selectedModifiers` (`frontend/contexts/CartContext.tsx` L194, L377).

---

## 5. Invalid modifier for product returns 400

**File:** `backend/Controllers/CartController.cs` (add-item), `backend/Services/ProductModifierValidationService.cs` (validation).

| Step | Location | Code reference |
|------|----------|-----------------|
| Validation service: only allowed modifiers returned | `ProductModifierValidationService.cs` L42–L67 | `GetAllowedModifiersWithPricesForProductAsync`: gets `allowedIds` via `GetAllowedModifierIdsForProductAsync(productId)`; loads only modifiers in `requestedModifierIds` that are in `allowedSet` (`toLoad = requestedSet.Where(id => allowedSet.Contains(id)).ToList()`). So invalid IDs are not in the result. |
| Controller: detect invalid requested IDs | `CartController.cs` L290–L296 | `validatedModifiers = await _modifierValidation.GetAllowedModifiersWithPricesForProductAsync(...)`; `validatedIds = validatedModifiers.Select(m => m.Id).ToHashSet()`; `invalidIds = requestedModifierIds.Where(id => !validatedIds.Contains(id)).ToList()` |
| Return 400 with message and invalid IDs | L294–L296 | `if (invalidIds.Count > 0) { ... return BadRequest(new { message = "One or more selected modifiers are not allowed for this product.", invalidModifierIds = invalidIds }); }` |

So any requested modifier ID that is not allowed for the product yields 400 and the list of invalid IDs.

---

## 6. FE optimistic total and backend returned total match

**Backend total (add-item response):**  
`BuildCartResponse` uses `ci.UnitPrice` (effective unit = product + sum modifier prices), then `CartMoneyHelper.ComputeLine(ci.UnitPrice, ci.Quantity, taxType)` and `ComputeCartTotals`. So `GrandTotalGross` = sum of `Round(unitPrice * quantity)` per line (with 2-decimal rounding). Backend does not use FE-supplied prices for modifiers; it uses DB-derived prices and stores them in `CartItem.UnitPrice` and `CartItemModifier.Price`.

**FE optimistic line total:**  
`frontend/contexts/CartContext.tsx`:

- L306–L308: `unitPrice = options?.unitPrice ?? 0` (base product price); `modifierTotal = modifiers.reduce((s, m) => s + m.price, 0)`; `lineUnitPrice = unitPrice + modifierTotal`.
- L309 (approx): `lineTotalPrice = lineUnitPrice * quantity` (no rounding in code; JS numbers).
- L342–L344: New item stored with `unitPrice`, `totalPrice: lineTotalPrice`, `modifiers`.

**FE after backend response:**  
L393–L396: `unitPrice: item.UnitPrice ?? item.unitPrice`, `totalPrice: item.TotalPrice || item.totalPrice` – so after add-item the FE replaces optimistic values with backend `UnitPrice` and `TotalPrice`.  
L415: `grandTotalGross: backendCart.GrandTotalGross ?? backendCart.grandTotalGross` – cart total is taken from backend.

So once the add-item response is applied, the FE displays the same totals as the backend (same `UnitPrice` and `TotalPrice` per line, same `grandTotalGross`). Optimistic and backend formulas align: both use (base unit price + sum modifier prices) × quantity; backend rounds at 2 decimals per line via `CartMoneyHelper.Round`, so after response the FE and backend totals match.

**Contract alignment note:** Backend add-item expects `selectedModifiers: [{ id: Guid }, ...]`. The POS currently sends `modifierIds: string[]` from `CartContext.tsx` L366 (`body.modifierIds = modifiers.map(m => m.id)`). For modifiers to be persisted and totals to include them, the FE should send `selectedModifiers: modifiers.map(m => ({ id: m.id }))` (or the backend could accept `modifierIds` and map to `SelectedModifiers`). With that alignment, the verification above holds end-to-end.

---

## Summary table

| Scenario | Verified | Key files / symbols |
|----------|----------|----------------------|
| add-item persists modifiers | Yes | `CartController.AddItemToCart` L285–L354; `CartItemModifier`; `AppDbContext` |
| add-item response totals include modifiers | Yes | `BuildCartResponse`; `CartMoneyHelper.ComputeLine(ci.UnitPrice, ...)`; `ComputeCartTotals` |
| GET current returns selectedModifiers | Yes | `GetCurrentUserCart` Include Modifiers; `BuildCartResponse` mapping `ci.Modifiers` → `SelectedModifiers` |
| table-orders-recovery restores selectedModifiers | Yes | `GetTableOrdersForRecovery` Include Modifiers (TableOrder + Cart); `TableOrderItemInfo.SelectedModifiers` / cart item mapping |
| Invalid modifier → 400 | Yes | `GetAllowedModifiersWithPricesForProductAsync`; `invalidIds` + `BadRequest` in `AddItemToCart` |
| FE/backend total match after response | Yes | Backend: `UnitPrice`/totals from DB; FE: uses `item.TotalPrice`, `backendCart.GrandTotalGross` after add-item. Send `selectedModifiers` for E2E. |

---

## FE contract alignment (add-item)

The POS cart add-item flow now sends the backend contract exactly:

- **Request:** `AddItemToCartRequest` from `frontend/services/api/cartService.ts` with `selectedModifiers?: SelectedModifierInput[]` where each element has at least `id`.
- **Payload:** `selectedModifiers: modifiers.map(m => ({ id: m.id }))` — no `modifierIds` in the add-item request body.
- **Confirmation:** `modifierIds` is not used anywhere in the cart add-item flow. The only remaining `modifierIds` usages are in the **payment** flow (`PaymentModal.tsx`, `paymentService.ts`), which is a separate API contract.
