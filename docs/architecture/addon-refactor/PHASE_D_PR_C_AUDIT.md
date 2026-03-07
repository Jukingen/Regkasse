# Phase D PR-C: Narrow POS Modifier-Group Contract (No Legacy Modifiers)

**Goal:** Reduce the POS-facing modifier group contract so the active POS path no longer depends on legacy `Modifiers` at the contract level. Admin must continue to receive `Modifiers` for migration and display.

**Context:** Phase C runtime uses only `group.products`; PR-B removed selectedModifiers from add-item write path. Frontend POS already maps `modifiers: []` in catalog and getProductModifierGroups. Backend still populates `ModifierGroupDto.Modifiers` for all endpoints.

---

## 1. Backend DTOs and Mappers Exposing ModifierGroupDto.Modifiers

| File | What |
|------|------|
| **backend/DTOs/ModifierDTOs.cs** | `ModifierGroupDto.Modifiers` (List<ModifierDto>), marked `[Obsolete]`. Single shared DTO for both POS and admin. |
| **backend/Controllers/ProductController.cs** | `MapToModifierGroupDto(ProductModifierGroup g)` (lines 236–266): sets `Modifiers = (g.Modifiers ?? …).Select(m => new ModifierDto { … }).ToList()`. Used by GetCatalog and GetProductModifierGroups. |
| **backend/Controllers/ModifierGroupsController.cs** | `MapToModifierGroupDto(ProductModifierGroup g)` (lines 365–401): same shape, includes `IsActive` on ModifierDto. Used by GetAll and GetById. |
| **backend/Controllers/AdminProductsController.cs** | `MapToModifierGroupDto(ProductModifierGroup g)` (lines 388–417): same shape (no IsActive on ModifierDto in mapping). Used by GetProductModifierGroups. |

All three controllers use the same DTO type (`ModifierGroupDto`); only the mapper implementation differs slightly (IsActive). All populate `Modifiers` from `g.Modifiers`.

---

## 2. Endpoints Used by Active POS That Return Modifiers

| Endpoint | Controller | Used by | Returns |
|----------|------------|---------|---------|
| **GET /Product/catalog** | ProductController | POS: `getProductCatalog()` → useProductsUnified → product list with modifierGroups | CatalogResponseDto.Products[].ModifierGroups[].Modifiers populated |
| **GET /Product/{id}/modifier-groups** | ProductController | POS: `getProductModifierGroups(productId)` (productModifiersService) when opening sheet per product | List<ModifierGroupDto> with Modifiers populated |

POS frontend already ignores `Modifiers` at mapping time (productService.mapModifierGroup sets `modifiers: []`; productModifiersService.mapGroupForPOS sets `modifiers: []`). So behavior is unchanged if backend stops sending Modifiers for these two endpoints; contract and payload size are reduced.

---

## 3. Admin Flows That Depend on Modifiers

| Endpoint | Controller | Used by | Usage |
|----------|------------|---------|--------|
| **GET /api/modifier-groups** | ModifierGroupsController | Admin: modifierGroups.ts `getModifierGroups()` | frontend-admin modifier-groups/page.tsx: `g.modifiers` for list and migration UI |
| **GET /api/modifier-groups/{id}** | ModifierGroupsController | Admin: single group detail | Same: legacy modifiers display and migrate action |
| **GET /api/admin/products/{productId}/modifier-groups** | AdminProductsController | Admin: products.ts `getProductModifierGroups(productId)` | ExtraZutatenSection.tsx: `group.modifiers` for display and migration |

Admin explicitly reads `group.modifiers` in:
- **frontend-admin/src/app/(protected)/modifier-groups/page.tsx** (lines 209, 264, 268): `const modifiers = g.modifiers ?? [];` and rendering/migrate.
- **frontend-admin/src/features/products/components/ExtraZutatenSection.tsx** (lines 53, 85, 89): `const modifiers = group.modifiers ?? [];` and rendering.

These must keep receiving `Modifiers` in the response.

---

## 4. Safest Way to Narrow the POS Contract Without Breaking Admin

- **Do not** remove or change `ModifierGroupDto` in the shared DTOs: admin needs `Modifiers`.
- **Do** stop populating `Modifiers` (or always return empty) **only for POS-facing endpoints**. That implies mapper specialization in **ProductController** only; leave ModifierGroupsController and AdminProductsController mappers unchanged.
- **Result:**  
  - POS: GET /Product/catalog and GET /Product/{id}/modifier-groups return modifier groups with `Modifiers` = [] (or omit the property; JSON typically still has `modifiers: []` if the DTO property is an empty list).  
  - Admin: GET /api/modifier-groups, GET /api/modifier-groups/{id}, GET /api/admin/products/{id}/modifier-groups continue to return full `Modifiers` for legacy display and migration.

No change to admin code or to shared DTO type; only the **source** of the response (which mapper is used) differs by endpoint.

---

## 5. Approach Comparison

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **POS-specific DTO** | New type e.g. ModifierGroupForPosDto without Modifiers; ProductController returns it. | Contractually no Modifiers for POS. | CatalogResponseDto / shared types must reference different group type or duplicate; two DTOs to maintain; possible serialization differences. |
| **Endpoint split** | New routes e.g. GET /Product/catalog/pos, GET /Product/{id}/modifier-groups/pos that return groups without Modifiers. | Clear separation. | Duplicate endpoints; frontend must switch URLs; more surface area. |
| **Response versioning** | e.g. Accept header or ?version=pos to return different shape. | Single URL. | More complex; harder to document and test; versioning overhead. |
| **Mapper specialization** | In ProductController only, use a mapper that sets Modifiers = empty list (or does not load/populate Modifiers). Same ModifierGroupDto; admin endpoints keep current mapper. | Minimal change; no new DTOs or routes; admin untouched; POS contract effectively “no modifiers” because response always has empty list. | ModifierGroupDto still has the property; POS clients could theoretically read it (they already ignore it). |

**Recommendation: Mapper specialization.** It is the smallest, safest change: only ProductController changes; POS endpoints return the same DTO type with `Modifiers` always empty; admin endpoints and admin UI stay as-is.

---

## Exact Files and Endpoints Involved

### Backend (implementation)

| File | Change |
|------|--------|
| **backend/Controllers/ProductController.cs** | Add `MapToModifierGroupDtoForPos(ProductModifierGroup g)` that returns ModifierGroupDto with same fields as today except `Modifiers = new List<ModifierDto>()`. Use it in GetCatalog (where modifier groups are built for each product) and in GetProductModifierGroups. Optionally drop `.Include(g => g.Modifiers.Where(...))` for the query path that only serves these two actions to avoid loading Modifiers for POS (small perf gain). |

### Backend (no change)

| File | Reason |
|------|--------|
| backend/DTOs/ModifierDTOs.cs | Keep ModifierGroupDto.Modifiers for admin. |
| backend/Controllers/ModifierGroupsController.cs | Admin; keep existing MapToModifierGroupDto and Include(Modifiers). |
| backend/Controllers/AdminProductsController.cs | Admin; keep existing MapToModifierGroupDto and Include(Modifiers). |

### Frontend POS (optional / follow-up)

| File | Note |
|------|------|
| frontend/services/api/productService.ts | Already sets `modifiers: []` in mapModifierGroup; no change required. |
| frontend/services/api/productModifiersService.ts | Already sets `modifiers: []` in mapGroupForPOS; can optionally narrow type to `modifiers?: ModifierDto[]` or keep for compat. |
| frontend type ModifierGroupDto | Can document that POS endpoints return modifiers: []; no structural change required. |

### Frontend Admin (no change)

| File | Reason |
|------|--------|
| frontend-admin modifier-groups/page.tsx, ExtraZutatenSection.tsx, lib/api/modifierGroups.ts | Continue to call admin endpoints; responses still include Modifiers. |

### Endpoints summary

| Endpoint | Returns Modifiers after PR-C |
|----------|------------------------------|
| GET /Product/catalog | No (empty list from MapToModifierGroupDtoForPos) |
| GET /Product/{id}/modifier-groups | No (empty list) |
| GET /api/modifier-groups | Yes (unchanged) |
| GET /api/modifier-groups/{id} | Yes (unchanged) |
| GET /api/admin/products/{id}/modifier-groups | Yes (unchanged) |

---

## Minimal Safe PR-C Plan

1. **ProductController**
   - Add a private static method `MapToModifierGroupDtoForPos(ProductModifierGroup g)` that builds ModifierGroupDto like the existing mapper but sets `Modifiers = new List<ModifierDto>()` (no mapping from `g.Modifiers`).
   - In **GetCatalog**: when building modifier groups for products, call `MapToModifierGroupDtoForPos` instead of `MapToModifierGroupDto`.
   - In **GetProductModifierGroups**: call `MapToModifierGroupDtoForPos` instead of `MapToModifierGroupDto`.
   - Optional: in the GetCatalog and GetProductModifierGroups query paths, remove `.Include(g => g.Modifiers.Where(m => m.IsActive))` so POS does not load Modifiers from DB (leave Include for AddOnGroupProducts and Product). Reduces payload and DB work; verify no other code in those actions needs g.Modifiers.

2. **Tests**
   - Existing tests that assert on catalog or product modifier-groups response: ensure they accept empty Modifiers (or add a POS-specific test that catalog / Product modifier-groups return no modifiers).
   - Phase2DtoCompatibilityTests or similar: if they assert on ProductController responses containing Modifiers, update expectations to empty list for POS endpoints.

3. **Documentation**
   - In ProductController, add a short comment that POS catalog and GetProductModifierGroups return modifier groups without legacy Modifiers (mapper specialization for Phase D PR-C).
   - Optionally add a line in PHASE_D_PR_B_SUMMARY or addon-refactor docs that PR-C narrows the POS modifier-group contract to products-only.

4. **No changes**
   - ModifierGroupsController, AdminProductsController, shared DTOs, admin frontend, POS frontend (already ignores Modifiers).

---

## Risk and Rollback

- **Risk:** Low. POS already ignores Modifiers; admin unchanged. Only ProductController response shape for two endpoints changes (Modifiers become empty).
- **Rollback:** Revert ProductController to use `MapToModifierGroupDto` for catalog and GetProductModifierGroups and, if removed, restore `.Include(g => g.Modifiers)` for those queries.
