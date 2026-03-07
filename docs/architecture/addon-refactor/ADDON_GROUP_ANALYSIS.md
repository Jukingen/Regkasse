# Add-on Group Behavior: FE-Admin & FE-POS Analysis

**Date:** 2025-03  
**Scope:** Responsibilities of Products vs Add-on-Gruppen screens, domain semantics, FE-POS source of truth, group name editability, and safest fix plan.

---

## 1. Map of the Two Admin Screens

### A. Products Page

| Item | Detail |
|------|--------|
| **Component** | `frontend-admin/src/features/products/components/ProductForm.tsx` — loads all groups via `getModifierGroups()`, loads assigned groups via `getProductModifierGroups(initialValues.id)`, uses `ExtraZutatenSection` with `selectedGroupIds` / `onChange={setSelectedModifierGroupIds}`. On submit, `modifierGroupIds: selectedModifierGroupIds` is sent. |
| **API** | **POST** (not PUT) `/api/admin/products/{productId}/modifier-groups` with body `{ modifierGroupIds: string[] }`. Implemented in `frontend-admin/src/api/admin/products.ts` as `setAdminProductModifierGroups(productId, modifierGroupIds)`. |
| **User-visible meaning** | “Which modifier groups (Extra Zutaten) are available for this product.” This is **PRODUCT → MODIFIER GROUP** assignment: the product is linked to one or more groups; at POS, those groups (and their contents) are offered as extras for that product. |

**Flow:** Create/Edit Product → Extra Zutaten section → select groups from list → Save → backend replaces `ProductModifierGroupAssignment` rows for that product.

---

### B. Add-on-Gruppen Page

| Item | Detail |
|------|--------|
| **Component** | `frontend-admin/src/app/(protected)/modifier-groups/page.tsx`. Lists groups from `getModifierGroups()`; “+ Produkt” opens a modal to add either an existing product or a new add-on (name, price, taxType, categoryId, sortOrder) to the selected group. |
| **API for adding products to group** | **POST** `/api/modifier-groups/{groupId}/products` with body either `{ productId: string }` or `{ createNewAddOnProduct: { name, price, taxType, categoryId, sortOrder } }`. Implemented in `frontend-admin/src/lib/api/modifierGroups.ts` as `addProductToGroup(groupId, body)`. So **MODIFIER GROUP → ADD-ON PRODUCTS** is correctly implemented: this page manages which sellable products (or new add-ons) belong to each group. |
| **Group metadata (name, sortOrder, isActive)** | **Backend:** `ModifierGroupsController` exposes **PUT** `/api/modifier-groups/{id}` with `CreateModifierGroupRequest` (Name, MinSelections, MaxSelections, IsRequired, SortOrder). So the backend supports updating group name and sortOrder. **FE-Admin:** The Add-on-Gruppen page has **no UI to edit an existing group**. It has “Gruppe anlegen” (create) and “+ Produkt” (add product to group) only. So **group names (and sortOrder, etc.) are not editable in the UI**; the backend endpoint exists but is unused by the admin. |

**Summary:** Add-on-Gruppen correctly manages group → products; what’s missing is **edit group** (name/sortOrder) calling the existing PUT.

---

## 2. Domain Semantics: “Ketchup” / “Mayo” as Group vs Product

**Current possibility:**

- Names like “Ketchup” and “Mayo” can appear as **modifier group names** (one group per sauce) if an operator created groups with those names and then assigned those groups to products.
- In the intended **add-on = product** architecture, they should be **sellable add-on products** inside a single group (e.g. “Saucen”), and the **group** should have a category-like name (“Saucen”, “Extras”), not the product name.

**So:**

- **Semantic modeling issue:** If “Ketchup” and “Mayo” are used as **group** names, that’s a domain modeling confusion: the group is the container; the products inside (Ketchup, Mayo) are the add-ons.
- **Recommended structure:** Group = “Saucen” (or “Extras”); Products = “Ketchup”, “Mayo” (each a Product with `IsSellableAddOn = true`, linked via `AddOnGroupProduct`). That matches the codebase design (group has many products; product has many assigned groups).

**Conclusion:** This is partly a **data/UX guidance** issue (how groups are named and used). Fixes: (1) ensure POS and admin get **group.products** so add-ons show up; (2) allow **editing group name** in admin so mis-named groups (e.g. “Ketchup” as group) can be renamed to “Saucen” and the actual add-ons managed as products inside that group.

---

## 3. FE-POS Source of Truth and Why Add-ons Don’t Show

### Where POS gets modifier groups

- **Primary:** Catalog from **GET** `/Product/catalog` (see `frontend/services/api/productService.ts`: `getProductCatalog()`). Response is mapped so each product has `modifierGroups: (p.ModifierGroups ?? p.modifierGroups).map(mapModifierGroup)`. So **catalog is the main source** for product → modifier groups on the POS.
- **Fallback:** When the bottom sheet is used without pre-loaded groups, it calls **GET** `/Product/{id}/modifier-groups` via `getProductModifierGroups(productId)` in `frontend/services/api/productModifiersService.ts`.

### What the backend actually returns

- **ProductController.GetCatalog()** (`backend/Controllers/ProductController.cs`):
  - Loads groups with `.Include(g => g.Modifiers.Where(m => m.IsActive))` **only** — it does **not** include `AddOnGroupProducts` or `Product`.
  - Uses a local `MapToModifierGroupDto(ProductModifierGroup g)` (lines 227–244) that sets only `Modifiers`; it **does not set `Products`**. So every group in the catalog has **empty or default `Products`**.
- **ProductController.GetProductModifierGroups()** (lines 501–534):
  - Same: loads groups with `Include(g => g.Modifiers)` only, no `AddOnGroupProducts`.
  - Builds `ModifierGroupDto` with only `Modifiers`; **`Products` is never set**.
- **AdminProductsController.GetProductModifierGroups()** (lines 290–324): Same pattern; no `Products`.
- **ModifierGroupsController.GetAll() / GetById()** (used by Add-on-Gruppen page): Correctly include `AddOnGroupProducts` and `ThenInclude(Product)`, and use `MapToModifierGroupDto` which **does** set `Products`. So the Add-on-Gruppen page sees `group.products`; catalog and product modifier-group endpoints do not.

### What POS renders

- **ProductGridCard** and **ProductRow** (`frontend/components/ProductGridCard.tsx`, `ProductRow.tsx`):
  - They use **both** `group.products` (add-on products) and `group.modifiers` (legacy).
  - `allAddOnProducts = groups.flatMap((g) => g.products ?? [])` → if `products` is always empty from catalog, **no add-on chips are shown**.
  - `allModifiers = groups.flatMap((g) => (g.modifiers ?? []).map(...))` → legacy modifiers still show if present.
- **ModifierSelectionBottomSheet** (`frontend/components/ModifierSelectionBottomSheet.tsx`):
  - Renders **only** `group.modifiers` (lines 98–100, 148: `group.modifiers.map(...)`). It **does not render `group.products`** at all. So even if the backend sent `products`, the bottom sheet would not show add-on products as selectable options; it is legacy-modifier only.

### Root cause of “groups have products: []” and inconsistent POS behavior

- **Immediate cause:** Catalog (and GET product modifier-groups) do **not** load or map `AddOnGroupProducts` → `ModifierGroupDto.Products`. So:
  - Product → group assignment exists (Products page).
  - Group → products is correctly managed on Add-on-Gruppen and stored in DB.
  - But the **catalog and per-product modifier-group API** never expose `group.products` to the client.
- **Result:** POS receives groups with `products: []` and only legacy `modifiers` (if any). So add-on products don’t appear as chips; only legacy modifiers do. If a group has no legacy modifiers and only add-on products, the group appears “empty” on POS.

---

## 4. Group Name Editability

| Layer | Status |
|-------|--------|
| **Backend** | **Update exists:** **PUT** `/api/modifier-groups/{id}` in `ModifierGroupsController.Update()` (lines 124–148). Accepts `CreateModifierGroupRequest` (Name, MinSelections, MaxSelections, IsRequired, SortOrder). |
| **FE-Admin** | **No UI.** The modifier-groups page has no “Edit group” / “Rename” action. So names like “Ketchup” / “Mayo” cannot be corrected to e.g. “Saucen” without DB or API tooling. |
| **Conclusion** | Backend supports it; the missing piece is a small admin UI that calls the existing PUT (e.g. edit group name/sortOrder in a modal or inline). |

---

## 5. Safest Fix Plan

### Root causes (concise)

1. **Backend (data):** Catalog and GET product modifier-groups do not load `AddOnGroupProducts` and do not fill `ModifierGroupDto.Products` → POS always sees `products: []`.
2. **FE-POS (rendering):** ProductGridCard/ProductRow already use `group.products`; once the API returns them, add-on chips will show. **ModifierSelectionBottomSheet** only shows `group.modifiers`; for add-on=product architecture, it should also show `group.products` (and trigger add-as-line behavior), but that can be a follow-up.
3. **FE-Admin (UX):** No way to edit group name/sortOrder; backend PUT exists.
4. **Domain (modeling):** Using “Ketchup”/“Mayo” as group names is a semantic confusion; preferred: group = “Saucen”, products = Ketchup, Mayo. Fixing (1)–(3) allows correcting data and UX.

### Files / endpoints involved

| Area | File(s) | Purpose |
|------|---------|---------|
| Catalog groups | `backend/Controllers/ProductController.cs` | GetCatalog(): add Include AddOnGroupProducts + Product; MapToModifierGroupDto(): set Products from AddOnGroupProducts. |
| Product modifier-groups (POS) | `backend/Controllers/ProductController.cs` | GetProductModifierGroups(): same Include + DTO mapping for Products. |
| Admin product modifier-groups | `backend/Controllers/AdminProductsController.cs` | GetProductModifierGroups(): same Include + DTO mapping for Products. |
| Group update (existing) | `backend/Controllers/ModifierGroupsController.cs` | PUT already implemented; no change. |
| FE-Admin edit group | `frontend-admin/src/app/(protected)/modifier-groups/page.tsx` | Add “Edit” (e.g. on group row) → modal with name, sortOrder → call PUT. |
| FE-Admin API | `frontend-admin/src/lib/api/modifierGroups.ts` | Add `updateModifierGroup(groupId, body)` calling PUT. |
| POS bottom sheet (optional later) | `frontend/components/ModifierSelectionBottomSheet.tsx` | Extend to show `group.products` and treat as add-on line (e.g. onAddAddOn). |

### Implementation order (safest first)

1. **Backend: Populate `Products` in catalog and product modifier-group responses**
   - In `ProductController`: GetCatalog() and GetProductModifierGroups():  
     - Include `AddOnGroupProducts` and `ThenInclude(a => a.Product)` when loading groups.  
     - In the DTO mapping (shared or inline), set `Products` from `g.AddOnGroupProducts` (same shape as in `ModifierGroupsController.MapToModifierGroupDto`).
   - In `AdminProductsController`: GetProductModifierGroups(): same Include + DTO mapping.
   - **Risk:** Low. Only adding data to existing DTOs; no contract or DB change. POS already consumes `group.products` for chips.
2. **FE-Admin: Edit group (name, sortOrder)**
   - Add `updateModifierGroup(groupId, { name, sortOrder, ... })` in `modifierGroups.ts` (PUT).
   - On Add-on-Gruppen page, add “Edit” (or pencil) per group → modal with form → submit calls updateModifierGroup and invalidates query.
   - **Risk:** Low. New UI and one new API wrapper; backend already supports PUT.
3. **FE-POS: ModifierSelectionBottomSheet and add-on products (optional, later)**
   - If “Edit” flow on POS should offer add-on products (not only legacy modifiers), extend the bottom sheet to list `group.products` and call an “add as line” callback (e.g. onAddAddOn). Requires design decision (single flow for modifiers + add-ons vs. chips-only for add-ons).

### What must NOT be changed (for this analysis)

- Do **not** remove or break product → group assignment (Products page, POST admin/products/{id}/modifier-groups).
- Do **not** remove or break group → products (Add-on-Gruppen, POST modifier-groups/{id}/products).
- Do **not** remove legacy `Modifiers` from DTOs or responses (backward compatibility).
- Do **not** change API route or payload for setProductModifierGroups (POST with modifierGroupIds).
- Do **not** add a new migration or DB schema change for this fix.

### Bug classification

| Issue | Type |
|-------|------|
| Catalog / GET modifier-groups not returning `group.products` | **Backend bug** (missing Include + DTO mapping). |
| POS shows no add-on chips for groups that have only products | **Consequence of above** (data problem). |
| No way to rename a group in admin | **FE-Admin UX gap** (backend exists). |
| ModifierSelectionBottomSheet ignores `group.products` | **FE-POS rendering gap** (optional to fix after backend). |
| “Ketchup”/“Mayo” as group names | **Domain/data modeling** (correctable via edit-group and guidance). |

---

## 6. Minimal Backend Change (for implementer)

To fix the “empty products” issue, the three places that return `ModifierGroupDto` for products (catalog and two GET modifier-groups) must:

1. **Include** when loading groups:
   - `.Include(g => g.AddOnGroupProducts).ThenInclude(a => a.Product)`
2. **Map** to DTO:
   - For each group, set `Products = (g.AddOnGroupProducts ?? []) .OrderBy(a => a.SortOrder) .Where(a => a.Product != null && a.Product.IsActive) .Select(a => new AddOnGroupProductItemDto { ProductId = a.ProductId, ProductName = a.Product.Name, Price = a.Product.Price, TaxType = a.Product.TaxType, SortOrder = a.SortOrder }).ToList()`  
   (or reuse the same logic as `ModifierGroupsController.MapToModifierGroupDto` to avoid duplication).

After this, catalog and both GET modifier-group endpoints will return `group.products`; POS ProductGridCard and ProductRow will show add-on product chips without any frontend change.
