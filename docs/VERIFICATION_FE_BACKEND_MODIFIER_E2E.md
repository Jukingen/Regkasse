# End-to-End Verification: POS Add Product with Modifiers → Payload → Backend Totals → Refresh → Recovery → UI

Exact code references for the scenario: Open POS → Add product with modifiers → Select one modifier (e.g. Ketchup) → Inspect add-item payload → Confirm backend totals include modifier → Refresh → Confirm table-orders-recovery returns selectedModifiers → Confirm FE hydrates and shows in cart/UI.

---

## 1. Open POS

| Step | File | Symbol / Location |
|------|------|-------------------|
| POS tab | `frontend/app/(tabs)/cash-register.tsx` | Default tab for cash register; component renders `ProductList`, `TableSelector`, `CartDisplay`, `CartSummary`. |
| Cart + recovery hooks | `cash-register.tsx` L125–144 | `useCart()` → `addItem`, `getCartForTable`, `toggleExtraOnCartItem`, etc.; `useTableOrdersRecoveryOptimized()` → `recoveryData`, `isRecoveryCompleted`. |
| Cart for active table | `cash-register.tsx` L149 | `const cart = getCartForTable(activeTableId);` — cart comes from `CartContext` state `cartsByTable[activeTableId]`. |

---

## 2. Add a product with modifiers, select one modifier (e.g. Ketchup)

| Step | File | Symbol / Location |
|------|------|-------------------|
| Product tap + modifiers | `frontend/app/(tabs)/cash-register.tsx` L46–69 | `usePOSOrderFlow`: `handleAddProduct(product, modifiers)` calls `addItem(product.id, 1, { modifiers, productName: product.name, unitPrice: product.price ?? 0 })`. |
| Modifier selection (chip) | `cash-register.tsx` L70–82 | `handleToggleModifier(productId, modifier)` — if a cart line exists for that product, calls `toggleExtraOnCartItem(cartItemId, modifier)`; else updates `pendingModifiersByProduct` so the next add includes that modifier. |
| Add-item entry point | `frontend/contexts/CartContext.tsx` L298–310 | `addItem(productId, quantity = 1, options?: { modifiers?, productName?, unitPrice? })`; `const modifiers = options?.modifiers ?? []`; then builds optimistic cart and request body. |

---

## 3. Inspect POST /api/cart/add-item payload — confirm `selectedModifiers: [{ id }]` is sent

| Step | File | Symbol / Location |
|------|------|-------------------|
| Request body type | `frontend/services/api/cartService.ts` L52–62 | `AddItemToCartRequest`: `productId`, `quantity`, `tableNumber?`, `waiterName?`, `notes?`, `selectedModifiers?: SelectedModifierInput[]`. `SelectedModifierInput`: `id` required; `name?`, `price?`, `groupId?` optional. |
| Body construction | `frontend/contexts/CartContext.tsx` L360–368 | `const body: AddItemToCartRequest = { productId, quantity, tableNumber: activeTableId };` then `if (modifiers.length) { body.selectedModifiers = modifiers.map(m => ({ id: m.id })); }`. No `modifierIds`. |
| HTTP call | `CartContext.tsx` L370 | `const response = await apiClient.post<AddItemResponse>('/cart/add-item', body);` — payload is exactly `AddItemToCartRequest` with `selectedModifiers: [{ id: "..." }]` when user selected e.g. Ketchup. |

**Payload shape (with one modifier):**
```json
{
  "productId": "<uuid>",
  "tableNumber": 1,
  "quantity": 1,
  "selectedModifiers": [{ "id": "<ketchup-modifier-uuid>" }]
}
```

---

## 4. Confirm backend response totals include modifier amount

| Step | File | Symbol / Location |
|------|------|-------------------|
| Backend: read request | `backend/Controllers/CartController.cs` L285–286 | `AddItemToCart`: `requestedModifierIds = (request.SelectedModifiers ?? new List<...>()).Select(s => s.Id).Distinct().ToList()`. |
| Backend: validate + price from DB | `CartController.cs` L288–301 | `validatedModifiers = await _modifierValidation.GetAllowedModifiersWithPricesForProductAsync(request.ProductId, requestedModifierIds)`; `effectiveUnitPrice = product.Price + validatedModifiers.Sum(m => m.Price)`; rounded to 2 decimals. |
| Backend: persist item + modifiers | `CartController.cs` L327–347, L354 | New `CartItem` with `UnitPrice = effectiveUnitPrice`; for each `validatedModifiers` add `CartItemModifier` (CartItemId, ModifierId, Name, Price); `await _context.SaveChangesAsync()`. |
| Backend: reload cart with modifiers | `CartController.cs` L356–360 | `updatedCart = await _context.Carts.Include(c => c.Items).ThenInclude(i => i.Modifiers).FirstOrDefaultAsync(...)`. |
| Backend: build response totals | `CartController.cs` L375 | `cartResponse = BuildCartResponse(updatedCart, products)`. |
| BuildCartResponse: line + totals | `CartController.cs` L1425–1489 | `BuildCartResponse`: for each `ci`, `line = CartMoneyHelper.ComputeLine(ci.UnitPrice, ci.Quantity, taxType)` — `ci.UnitPrice` already includes modifiers; `totals = CartMoneyHelper.ComputeCartTotals(lineAmounts)`; response sets `SubtotalGross`, `SubtotalNet`, `IncludedTaxTotal`, `GrandTotalGross`, and per-item `TotalPrice`, `LineNet`, `LineTax`. |

So the add-item response’s item totals and cart totals include the modifier amount because `UnitPrice` is effective (product + modifiers) and `BuildCartResponse` uses it for all line and cart calculations.

---

## 5. FE replaces optimistic values with backend item/cart totals

| Step | File | Symbol / Location |
|------|------|-------------------|
| Response handling | `frontend/contexts/CartContext.tsx` L372–418 | On `response.cart`, map `backendItems` to `localItems`: for each item use `item.UnitPrice`, `item.TotalPrice`, `item.SelectedModifiers ?? item.selectedModifiers ?? ...` (L378–379). |
| Modifier hydration from response | `CartContext.tsx` L377–402 | `mods = item.SelectedModifiers ?? item.selectedModifiers ?? item.Modifiers ?? item.modifiers`; if array, `modifierList = mods.map(m => ({ id: m.Id ?? m.id, name: m.Name ?? m.name, price: Number(m.Price ?? m.price ?? 0) }))`; each `localItems` entry gets `unitPrice`, `totalPrice`, `modifiers: modifierList`. |
| Cart totals from backend | `CartContext.tsx` L406–417 | `setCartsByTable` with `subtotalGross`, `subtotalNet`, `includedTaxTotal`, `grandTotalGross` from `backendCart.GrandTotalGross` etc. So cart UI total is backend total (includes modifiers). |

---

## 6. Refresh page

| Step | File | Symbol / Location |
|------|------|-------------------|
| App remount | — | On full page refresh (F5 or pull-to-refresh of the app), React tree remounts; `CartProvider` state is reinitialized (or restored from AsyncStorage if that runs first). |
| Recovery fetch on load | `frontend/hooks/useTableOrdersRecoveryOptimized.ts` L256–268 | `useEffect` when `user` is set: if not yet initialized, calls `fetchTableOrdersRecovery()`. |
| Table list from recovery | `useTableOrdersRecoveryOptimized.ts` L128–133 | `fetchTableOrdersRecovery`: `const response = await apiClient.get('/cart/table-orders-recovery');` — single GET that returns all active table orders. |
| Table selector item count (before table selected) | `frontend/components/TableSelector.tsx` L31–41 | `getTableItemCount(tableNumber)`: if `tableCarts.get(tableNumber)` is undefined, uses `recoveryData?.tableOrders?.find(order => order.tableNumber === tableNumber)` and returns `recoveryOrder?.itemCount ?? 0`. So after refresh, table badges can show counts from recovery until that table’s cart is loaded. |

---

## 7. Confirm GET /api/cart/table-orders-recovery returns selectedModifiers

| Step | File | Symbol / Location |
|------|------|-------------------|
| Backend: load carts with item modifiers | `backend/Controllers/CartController.cs` L1134–1141 | `GetTableOrdersForRecovery`: `userActiveCarts = await _context.Carts.Include(c => c.Items).ThenInclude(ci => ci.Modifiers).Where(...).ToListAsync()`. |
| Backend: load table orders with item modifiers | `CartController.cs` L1121–1131 | `userActiveTableOrders = await _context.TableOrders.Include(to => to.Items).ThenInclude(toi => toi.Modifiers).Where(...).ToListAsync()`. |
| Backend: map cart-based items to response | `CartController.cs` L1237–1255 | For each cart-based order, `Items = items.Zip(lineAmounts, (item, line) => new TableOrderItemInfo { ..., SelectedModifiers = (item.Modifiers ?? Enumerable.Empty<CartItemModifier>()).Select(m => new SelectedModifierDto { Id = m.ModifierId, Name = m.Name, Price = m.Price }).ToList() })`. |
| Backend: map TableOrder items to response | `CartController.cs` L1173–1186 | For each TableOrder, `Items = toItems.Zip(lineAmounts, (item, line) => new TableOrderItemInfo { ..., SelectedModifiers = (item.Modifiers ?? Enumerable.Empty<TableOrderItemModifier>()).Select(m => new SelectedModifierDto { ... }).ToList() })`. |

So the table-orders-recovery response includes, for each item, `selectedModifiers` (or `SelectedModifiers` in JSON) with `id`, `name`, `price` from the persisted `CartItemModifier` / `TableOrderItemModifier` rows.

---

## 8. Confirm FE hydrates and shows selectedModifiers in cart/UI

Two ways the cart gets data after refresh:

**A. User selects a table → GET /cart/current (primary path for cart content)**

| Step | File | Symbol / Location |
|------|------|-------------------|
| Table selection | `frontend/app/(tabs)/cash-register.tsx` L272–303 | `handleTableSelect(tableNumber)` → `switchTable(tableNumber)`. |
| Switch + fetch cart | `frontend/contexts/CartContext.tsx` L270–288 | `switchTable`: `setActiveTableId(tableNumber)` then `await fetchTableCart(tableNumber)`. |
| GET /cart/current | `CartContext.tsx` L168–181 | `fetchTableCart`: `const response = await apiClient.get(\`/cart/current?tableNumber=${tableNumber}\`)`. |
| Backend: current cart with modifiers | `backend/Controllers/CartController.cs` L46–54 | `GetCurrentUserCart`: cart loaded with `.Include(c => c.Items).ThenInclude(i => i.Modifiers)`; then `BuildCartResponse(cart, products)` which maps `ci.Modifiers` to `SelectedModifiers` (see §4). |
| FE: hydrate items and modifiers | `CartContext.tsx` L182–239 | `backendItems.map`: `backendMods = item.SelectedModifiers ?? item.selectedModifiers ?? item.Modifiers ?? item.modifiers`; if array, `modifierList = backendMods.map(m => ({ id: m.Id ?? m.id, name: m.Name ?? m.name, price: Number(m.Price ?? m.price ?? 0) }))`; each item gets `modifiers: modifierList` and `totalPrice` from backend or derived. |
| Cart state update | `CartContext.tsx` L226–239 | `setCartsByTable` for that `tableNumber` with `items: localItems` (including `modifiers`) and backend totals. |

**B. Recovery data type (table-orders-recovery response shape)**

| Step | File | Symbol / Location |
|------|------|-------------------|
| Recovery item type | `frontend/hooks/useTableOrdersRecoveryOptimized.ts` L10–18 | `TableOrderRecoveryItem`: `selectedModifiers?: Array<{ id: string; name: string; price: number }>`. So FE expects `selectedModifiers` on each recovery item. |
| Recovery used for table list | `TableSelector.tsx` L36–40 | When `tableCarts.get(tableNumber)` is undefined, item count comes from `recoveryData?.tableOrders?.find(...)?.itemCount`. Recovery data is not used to set `cartsByTable` items; cart items are set by `fetchTableCart` (GET /cart/current) when the user selects a table. |

**C. Cart UI shows modifiers**

| Step | File | Symbol / Location |
|------|------|-------------------|
| Cart passed to display | `frontend/app/(tabs)/cash-register.tsx` L433–434 | `cart={cart}` where `cart = getCartForTable(activeTableId)` — so cart is from `cartsByTable`, which was filled by add-item response or by `fetchTableCart` (GET /cart/current). |
| CartDisplay iterates items | `frontend/components/CartDisplay.tsx` L99–110 | `cart.items.map((item: CartItem)` — item has `modifiers?: { id; name; price }[]` (L20). |
| CartItemRow renders modifiers | `frontend/components/CartItemRow.tsx` L49–57 | `item.modifiers && item.modifiers.length > 0` → block with `item.modifiers.map((m) => <Text key={m.id}>+ {m.name}</Text>)` and extra price line. So hydrated `item.modifiers` are shown in the cart list. |

End-to-end: After refresh, user selects table → `fetchTableCart` runs GET /cart/current → backend returns items with `SelectedModifiers` → CartContext maps them to `modifiers` and updates `cartsByTable` → `cart` from `getCartForTable(activeTableId)` has `items[].modifiers` → CartDisplay → CartItemRow shows modifier names and prices.

---

## Summary table

| Check | Status | Where |
|-------|--------|--------|
| Add product + select modifier (e.g. Ketchup) | ✓ | `cash-register.tsx` handleAddProduct / handleToggleModifier → `CartContext.addItem` with `options.modifiers`. |
| POST /cart/add-item payload has `selectedModifiers: [{ id }]` | ✓ | `CartContext.tsx` L361–368: `body.selectedModifiers = modifiers.map(m => ({ id: m.id }))`; type `AddItemToCartRequest` in `cartService.ts`. |
| Backend response totals include modifier | ✓ | `CartController.AddItemToCart`: effectiveUnitPrice = product + modifiers; BuildCartResponse uses `ci.UnitPrice` and ComputeCartTotals. |
| After add-item, FE uses backend totals | ✓ | `CartContext.tsx` L372–417: map backend items (UnitPrice, TotalPrice, SelectedModifiers) and backend cart totals into state. |
| Refresh: table-orders-recovery called | ✓ | `useTableOrdersRecoveryOptimized` useEffect → `fetchTableOrdersRecovery` → GET `/cart/table-orders-recovery`. |
| table-orders-recovery returns selectedModifiers | ✓ | `CartController.GetTableOrdersForRecovery`: Include Modifiers for cart and TableOrder items; map to TableOrderItemInfo.SelectedModifiers. |
| FE hydrates cart from GET /cart/current after table select | ✓ | `switchTable` → `fetchTableCart` → GET /cart/current; map `item.SelectedModifiers ?? item.selectedModifiers` to `modifierList`; set `cartsByTable[tableNumber].items[].modifiers`. |
| Cart/UI shows modifiers | ✓ | `CartDisplay` → `CartItemRow`: `item.modifiers` rendered (names + extra price). |
