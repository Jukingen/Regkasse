# Add-on / Modifier Architecture — Implementation Plan

**Date:** 2025-03-07  
**Scope:** Migration from legacy modifiers to add-on = Product model. POS flat cart lines. Legacy kept for compatibility only.

---

## 1. Inventory: Entities, DTOs, APIs, Services, UI

### 1.1 Backend Entities

| Entity | Path | Role |
|--------|------|------|
| `Product` | `backend/Models/Product.cs` | Base sellable; `IsSellableAddOn` marks add-on products |
| `ProductModifierGroup` | `backend/Models/ProductModifierGroup.cs` | Container; has `Modifiers` (legacy) + `AddOnGroupProducts` (new) |
| `ProductModifier` | `backend/Models/ProductModifier.cs` | **Legacy** modifier (name, price, tax in own table) |
| `AddOnGroupProduct` | `backend/Models/AddOnGroupProduct.cs` | Group ↔ Product link; price from Product |
| `ProductModifierGroupAssignment` | `backend/Models/ProductModifierGroupAssignment.cs` | Product ↔ ModifierGroup (which groups for a product) |
| `CartItem` | `backend/Models/CartItem.cs` | Flat line; `Modifiers` = legacy read-only |
| `CartItemModifier` | `backend/Models/CartItemModifier.cs` | **Legacy** embedded modifier per cart line |
| `TableOrderItemModifier` | `backend/Models/TableOrderItemModifier.cs` | **Legacy** modifier per table order line |
| `PaymentItem` | `backend/Models/PaymentItem.cs` | Payment line; `Modifiers` = legacy read-only |
| `ReceiptItem` | `backend/Models/ReceiptItem.cs` | Receipt line; `ParentItemId` for legacy nested modifier lines |

### 1.2 Backend DTOs

| DTO | Path | Role |
|-----|------|------|
| `ModifierGroupDto` | `backend/DTOs/ModifierDTOs.cs` | `Products` (primary) + `Modifiers` (legacy) |
| `AddOnGroupProductItemDto` | `backend/DTOs/ModifierDTOs.cs` | productId, productName, price, taxType, sortOrder |
| `ModifierDto` | `backend/DTOs/ModifierDTOs.cs` | **Legacy** id, name, price, taxType |
| `SelectedModifierDto` | `backend/DTOs/ModifierDTOs.cs` | **Legacy** read-only in cart/table-order responses |
| `SelectedModifierInputDto` | `backend/DTOs/ModifierDTOs.cs` | **Legacy** add-item/update-item (accepted but ignored for write) |
| `CatalogProductDto` | `backend/DTOs/CatalogDTOs.cs` | Product + `ModifierGroups` (each with Products + Modifiers) |

### 1.3 Backend API Endpoints

| Endpoint | Controller | Role |
|----------|------------|------|
| `GET /api/Product/catalog` | ProductController | Catalog with modifier groups; **already includes** AddOnGroupProducts + Products |
| `GET /api/Product/{id}/modifier-groups` | ProductController | Product modifier groups; **already includes** AddOnGroupProducts |
| `POST /api/Product/{id}/modifier-groups` | ProductController | Set product → group assignments |
| `GET /api/modifier-groups` | ModifierGroupsController | All groups (admin); Products + Modifiers |
| `GET /api/modifier-groups/{id}` | ModifierGroupsController | Single group; Products + Modifiers |
| `PUT /api/modifier-groups/{id}` | ModifierGroupsController | Update group (name, sortOrder, etc.) |
| `POST /api/modifier-groups/{id}/products` | ModifierGroupsController | Add product to group |
| `DELETE /api/modifier-groups/{groupId}/products/{productId}` | ModifierGroupsController | Remove product from group |
| `POST /api/modifier-groups/{groupId}/modifiers` | ModifierGroupsController | **410 Gone** — legacy creation disabled |
| `POST /api/modifier-groups/{groupId}/modifiers/{modifierId}/migrate` | ModifierGroupsController | Migrate single legacy modifier → product |
| `POST /api/cart/add-item` | CartController | Add item; `IsSellableAddOn` → flat line; `SelectedModifiers` ignored for write |
| `PUT /api/cart/items/{itemId}` | CartController | Update item; `SelectedModifiers` ignored for write |
| `GET /api/admin/products/{id}/modifier-groups` | AdminProductsController | Admin product modifier groups |

### 1.4 Backend Services

| Service | Path | Role |
|---------|------|------|
| `ModifierMigrationService` | `backend/Services/ModifierMigrationService.cs` | Migrate legacy modifiers → add-on products |
| `ProductModifierValidationService` | `backend/Services/ProductModifierValidationService.cs` | Legacy modifier validation (allowed IDs, prices) |
| `PaymentService` | `backend/Services/PaymentService.cs` | Payment; add-ons = product-only lines; legacy modifier payload ignored |
| `ReceiptService` | `backend/Services/ReceiptService.cs` | Receipt from payment; legacy modifiers → nested ReceiptItems |
| `TableOrderService` | `backend/Services/TableOrderService.cs` | Table orders; legacy modifiers in TableOrderItemModifier |

### 1.5 Admin UI

| Component/Page | Path | Role |
|----------------|------|------|
| Modifier Groups Page | `frontend-admin/src/app/(protected)/modifier-groups/page.tsx` | CRUD groups; add/remove products; migrate single modifier; **edit group** (name, sortOrder) |
| ExtraZutatenSection | `frontend-admin/src/features/products/components/ExtraZutatenSection.tsx` | Product form: assign groups; shows `group.products` + `group.modifiers` (read-only) |
| ProductForm | `frontend-admin/src/features/products/components/ProductForm.tsx` | Uses ExtraZutatenSection; `modifierGroupIds` on submit |
| modifierGroups API | `frontend-admin/src/lib/api/modifierGroups.ts` | getModifierGroups, updateModifierGroup, addProductToGroup, removeProductFromGroup, migrateLegacyModifier |

### 1.6 POS (Frontend)

| Component | Path | Role |
|-----------|------|------|
| cash-register | `frontend/app/(tabs)/cash-register.tsx` | Main POS; `handleAddAddOn` → addItem(productId, 1) for add-ons |
| ProductList | `frontend/components/ProductList.tsx` | Product list; passes `onAddAddOn` to ProductRow/ProductGridCard |
| ProductRow | `frontend/components/ProductRow.tsx` | **Primary: group.products**; fallback: group.modifiers (legacy); chips for add-ons |
| ProductGridCard | `frontend/components/ProductGridCard.tsx` | Same as ProductRow |
| ModifierOptionChips | `frontend/components/ModifierOptionChips.tsx` | Chips for add-on products + legacy modifiers |
| ModifierSelectionBottomSheet | `frontend/components/ModifierSelectionBottomSheet.tsx` | **Only group.modifiers** — does NOT show group.products |
| ModifierSelectionModal | `frontend/components/ModifierSelectionModal.tsx` | **Only group.modifiers** — does NOT show group.products |
| CartContext | `frontend/contexts/CartContext.tsx` | addItem, addModifier; add-on = addItem(productId) with no modifiers |
| CartDisplay | `frontend/components/CartDisplay.tsx` | Renders cart; supports legacy modifiers per line |
| CartItemRow | `frontend/components/CartItemRow.tsx` | Renders item + legacy modifiers (increment/decrement/remove) |
| productService | `frontend/services/api/productService.ts` | getProductCatalog; maps modifierGroups |
| productModifiersService | `frontend/services/api/productModifiersService.ts` | getProductModifierGroups(productId) |

---

## 2. Current Dependencies on `group.modifiers` and Legacy Modifier Flow

### 2.1 Uses `group.modifiers` Only (No `group.products`)

| File | Line(s) | Behavior |
|------|---------|----------|
| `frontend/components/ModifierSelectionBottomSheet.tsx` | 98–100, 148 | `group.modifiers.map(...)` — add-on products never shown |
| `frontend/components/ModifierSelectionModal.tsx` | 132 | `group.modifiers.map(...)` — same |
| `ModifierSelectionBottomSheet` `getSelectedModifiers()` | 96–102 | Iterates only `g.modifiers` |

### 2.2 Uses Both `group.products` and `group.modifiers`

| File | Behavior |
|------|----------|
| `frontend/components/ProductRow.tsx` | Primary: `group.products`; fallback: `group.modifiers` when no products |
| `frontend/components/ProductGridCard.tsx` | Same |
| `frontend-admin/src/features/products/components/ExtraZutatenSection.tsx` | Renders both; products first, modifiers as "Legacy" |

### 2.3 Legacy Modifier Creation/Editing

| Area | Status |
|------|--------|
| Backend `POST .../modifiers` | **410 Gone** — creation frozen |
| Admin Add-on-Gruppen | No UI to create legacy modifiers; only add products |
| Admin Products | ExtraZutatenSection shows modifiers read-only |

### 2.4 Legacy Modifier in Cart/Payment/Receipt

| Area | Status |
|------|--------|
| Cart add-item | `SelectedModifiers` accepted but **ignored for write** (Phase 3 prep) |
| Cart update-item | Same |
| Cart response | `SelectedModifiers` still returned for legacy carts (read-only) |
| Payment | Modifier payload **ignored**; add-ons must be separate payment items |
| Receipt | Legacy `PaymentItem.Modifiers` → nested ReceiptItems (ParentItemId) |
| CartContext | Still sends `selectedModifiers` in add-item; backend ignores |
| CartContext | addModifier, incrementModifier, decrementModifier, removeModifier — for legacy embedded modifiers |

---

## 3. Implementation Plan — Three Sections

---

### Section A: Legacy Modifier Migration Tool

**Goal:** Complete migration path for operators to move all legacy modifiers to add-on products. Keep legacy entities for read-only compatibility.

#### Affected Files

| Layer | Files |
|-------|-------|
| Backend | `ModifierMigrationService.cs`, `IModifierMigrationService.cs`, `ModifierMigrationDTOs.cs`, `AdminMigrationController.cs` |
| Admin | `modifier-groups/page.tsx`, `modifierGroups.ts` |
| Tests | `ModifierMigrationServiceTests.cs` |

#### Required Backend Changes

- [x] **Bulk migration endpoint** — `POST /api/admin/migrate-legacy-modifiers` exists in AdminMigrationController; CLI: `dotnet run -- migrate-legacy-modifiers <CategoryId> [--dryrun]`
- [ ] **Single migration** already exists: `POST /api/modifier-groups/{groupId}/modifiers/{modifierId}/migrate`
- [ ] Ensure migration sets `IsSellableAddOn = true` on created products
- [ ] Ensure migration adds `AddOnGroupProduct` link and optionally marks modifier `IsActive = false`
- [ ] Add migration report endpoint: count of remaining legacy modifiers, migrated count, errors

#### Required Admin UI Changes

- [ ] Add "Bulk migration" action on Add-on-Gruppen page (or dedicated migration page) — category selector + dry-run + execute
- [ ] Show migration status: remaining legacy modifiers per group
- [ ] "Als Produkt migrieren" per modifier — **already exists** in modifier-groups page
- [ ] Optional: Hide or de-emphasize legacy modifier list once migration is complete

#### Required POS Changes

- [ ] None for migration tool (admin-only)

#### Migration Risks

- Idempotency: migration must not create duplicate products for same Name+Price in same group
- Category: new add-on products need valid CategoryId; migration fails if category missing
- Receipts/orders: legacy modifiers in historical data remain; no backfill of old receipts

#### Open Questions / Inconsistencies

- Is `POST /api/admin/migrate-legacy-modifiers` implemented? (Check AdminMigrationController)
- Should migration support "merge" when product already exists (e.g. same name, different price)?

---

### Section B: Add-on System Stabilization

**Goal:** Ensure `group.products` is the single source of truth for add-ons. Catalog and all modifier-group endpoints return products. POS uses products only for new flows.

#### Affected Files

| Layer | Files |
|-------|-------|
| Backend | `ProductController.cs` (GetCatalog, GetProductModifierGroups), `AdminProductsController.cs` (GetProductModifierGroups) |
| Admin | `modifierGroups.ts`, `modifier-groups/page.tsx`, `ExtraZutatenSection.tsx` |
| POS | `productService.ts`, `productModifiersService.ts`, `ProductRow.tsx`, `ProductGridCard.tsx` |

#### Required Backend Changes

- [x] **Catalog** — ProductController.GetCatalog already includes `AddOnGroupProducts` + `ThenInclude(Product)` and maps to `ModifierGroupDto.Products` ✅
- [x] **Product modifier-groups** — ProductController.GetProductModifierGroups already includes AddOnGroupProducts ✅
- [ ] **AdminProductsController.GetProductModifierGroups** — Verify it includes AddOnGroupProducts and maps Products (check if used by admin product form)
- [ ] Ensure `ModifierGroupDto.Modifiers` remains populated for legacy read (do not remove)
- [ ] Add `IsSellableAddOn` to CatalogProductDto if products need to be filtered/highlighted on POS

#### Required Admin UI Changes

- [x] **Edit group** — modifier-groups page already has `openEditGroup` + `handleEditGroup` calling `updateModifierGroup` ✅
- [ ] ExtraZutatenSection: Consider de-emphasizing or collapsing legacy modifiers section when products exist
- [ ] Products page: Ensure `getProductModifierGroups` returns `group.products` (admin API)

#### Required POS Changes

- [ ] **productService** — Verify catalog mapping includes `modifierGroups` with `products` (camelCase)
- [ ] **productModifiersService** — Types already have `products?: AddOnGroupProductItemDto[]`; ensure API returns them
- [ ] ProductRow/ProductGridCard: Already use `group.products` first; no change if API returns them
- [ ] **ModifierSelectionBottomSheet** — Extend to show `group.products` and treat as add-on (add as line via `onAddAddOn` callback) — **critical gap**

#### Migration Risks

- Low: Only adding/exposing data; no schema change
- Backward compat: Legacy modifiers still in response; old clients unaffected

#### Open Questions / Inconsistencies

- ADDON_GROUP_ANALYSIS.md stated catalog did not return products; code review shows ProductController.GetCatalog **does** include AddOnGroupProducts. Confirm in production.
- ModifierSelectionModal: Used where? If only in legacy flows, may deprecate instead of extending.

---

### Section C: POS Add-on UX Improvement

**Goal:** POS uses flat cart lines only. Add-on products added via chips or bottom sheet as separate lines. Legacy modifier UI kept for historical carts only.

#### Affected Files

| Layer | Files |
|-------|-------|
| POS | `ModifierSelectionBottomSheet.tsx`, `ModifierSelectionModal.tsx`, `cash-register.tsx`, `CartContext.tsx`, `CartDisplay.tsx`, `CartItemRow.tsx`, `ProductRow.tsx`, `ProductGridCard.tsx` |

#### Required Backend Changes

- [ ] None (cart/payment already support flat model)

#### Required Admin UI Changes

- [ ] None

#### Required POS Changes

- [ ] **ModifierSelectionBottomSheet** — Add `group.products` rendering:
  - For each product in `group.products`: show as selectable option
  - On select: call `onAddAddOn?.({ productId, productName, price })` instead of adding to modifier state
  - Requires new prop: `onAddAddOn?: (addOn: { productId: string; productName: string; price: number }) => void`
  - When `onAddAddOn` is provided, products add as lines; when not, keep legacy modifier behavior for backward compat
- [ ] **ModifierSelectionModal** — Same extension if still used
- [ ] **CartContext.addItem** — When adding add-on product, do NOT send `selectedModifiers` (already correct for handleAddAddOn)
- [ ] **CartDisplay / CartItemRow** — Keep legacy modifier display for items with `modifiers` (from backend); new add-ons appear as separate items
- [ ] **ProductRow / ProductGridCard** — Already use `onAddAddOn` for chips; ensure `onAddAddOn` is always passed from cash-register
- [ ] **UX decision:** When product has both `group.products` and `group.modifiers`:
  - Option A: Show only products (chips + bottom sheet); hide modifiers
  - Option B: Show both; products → add line, modifiers → legacy embedded (deprecated)
  - **Recommendation:** Option A after migration; Option B during transition

#### Migration Risks

- If ModifierSelectionBottomSheet is used in "Edit" flow: must ensure Edit adds add-ons as lines, not embedded modifiers
- CartContext still has addModifier/incrementModifier/etc.; these apply to legacy items. Do not remove.

#### Open Questions / Inconsistencies

- Where is ModifierSelectionBottomSheet opened? (Search for usage)
- Where is ModifierSelectionModal opened?
- ProductSelector.tsx uses `group.products` — confirm it's for a different flow (product search, not modifier selection)

---

## 4. Summary Checklist

### Legacy Modifier Migration Tool
- [ ] Verify bulk migration endpoint exists
- [ ] Add migration report (remaining count)
- [ ] Admin: Bulk migration UI
- [ ] Admin: Per-modifier "Als Produkt migrieren" — done

### Add-on System Stabilization
- [x] Catalog returns group.products
- [x] Product modifier-groups returns group.products
- [ ] Admin product modifier-groups returns group.products (verify)
- [x] Edit group UI exists
- [ ] ModifierSelectionBottomSheet: add group.products + onAddAddOn

### POS Add-on UX Improvement
- [ ] ModifierSelectionBottomSheet: render group.products, call onAddAddOn
- [ ] ModifierSelectionModal: same (if used)
- [ ] Ensure chips always use onAddAddOn for add-on products
- [ ] Document: legacy modifier UI only for items with modifiers (read-only)

---

## 5. Do Not Touch (Compliance)

- TSE, Receipt numbering, Daily closing, FinanzOnline mapping, Audit logging
- CartItemModifier, TableOrderItemModifier, PaymentItem.Modifiers, ReceiptItem parent/child — **keep for read-only legacy**
- Do not delete legacy modifier tables or DTOs
- Do not remove `SelectedModifiers` from cart/table-order API responses

---

## 6. References

- `docs/architecture/addon-refactor/ADDON_GROUP_ANALYSIS.md`
- `backend/docs/SELECTED_MODIFIERS_IMPLEMENTATION.md`
- `backend/docs/FIX_TABLE_ORDER_ITEM_MODIFIERS_503.md`
- `backend/docs/DTO_PLAN_CATALOG_AND_MODIFIERS.md`
