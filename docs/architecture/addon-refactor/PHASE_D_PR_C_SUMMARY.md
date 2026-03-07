# Phase D PR-C: POS Modifier-Group Contract (No Legacy Modifiers)

**Goal:** For POS-facing product modifier group responses, stop exposing legacy `Modifiers` in the response contract. Admin and historical compatibility unchanged.

**Approach:** Mapper specialization in ProductController only; same DTO type, empty `Modifiers` for POS endpoints.

---

## Touched files

### Backend

| File | Change |
|------|--------|
| **backend/Controllers/ProductController.cs** | Added `MapToModifierGroupDtoForPos(ProductModifierGroup g)` returning ModifierGroupDto with `Modifiers = new List<ModifierDto>()`. GetCatalog and GetProductModifierGroups use it; removed `.Include(g => g.Modifiers)` for those flows and removed legacy-modifier log for them. |

### Frontend (POS)

| File | Change |
|------|--------|
| **frontend/services/api/productService.ts** | Comment: catalog returns modifier groups with empty modifiers (Phase D PR-C). Product.modifierGroups comment updated. |
| **frontend/services/api/productModifiersService.ts** | Comment: Product modifier-groups endpoint returns empty modifiers. ModifierGroupDto/ModifierDto comments updated; removed outdated "Legacy (API still returns…)" block. |
| **docs/architecture/addon-refactor/PHASE_D_PR_C_SUMMARY.md** | This summary. |

---

## Contract changes

- **GET /Product/catalog:** Products[].ModifierGroups[].Modifiers is always an empty list. Backend does not load or map legacy Modifiers for this endpoint.
- **GET /Product/{id}/modifier-groups:** Each ModifierGroupDto.Modifiers is always an empty list. Backend does not load or map legacy Modifiers for this endpoint.
- POS frontend mappers already set `modifiers: []` and do not read legacy modifiers from the response; no POS code depends on group.Modifiers from these endpoints. Types and comments document that POS endpoints return empty modifiers and add-on UI uses `.products` only.

---

## Compatibility intentionally kept

- **ModifierGroupDto:** Unchanged; still has `Modifiers` for admin.
- **GET /api/modifier-groups** (ModifierGroupsController): Still loads and returns Modifiers for admin list/detail and migration UI.
- **GET /api/modifier-groups/{id}:** Same.
- **GET /api/admin/products/{id}/modifier-groups** (AdminProductsController): Still loads and returns Modifiers for ExtraZutatenSection and migration.
- **Admin frontend:** No code changes; continues to use `group.modifiers` from admin endpoints.
- **MapToModifierGroupDto** in ProductController: Kept; GetCatalog and GetProductModifierGroups use only `MapToModifierGroupDtoForPos`.
- **Cart/order item.modifiers:** Unchanged; historical cart and receipt flows still read item-level modifiers from cart/table-order/payment responses (not from modifier-group API).

---

## Verification

- **POS-facing modifier groups:** ProductController GetCatalog and GetProductModifierGroups use `MapToModifierGroupDtoForPos` only; no `.Include(g => g.Modifiers)` in those paths. Responses do not rely on legacy Modifiers.
- **Admin flows:** ModifierGroupsController and AdminProductsController still use `MapToModifierGroupDto` and `.Include(g => g.Modifiers)`; admin modifier-groups list, detail, and product ExtraZutatenSection continue to receive and display Modifiers.
- **Dead code removed:** POS productModifiersService outdated "Legacy (API still returns…)" comment block removed; ModifierDto comment simplified (type kept for DTO shape). No other POS code was reading group.modifiers from catalog/modifier-groups responses.

---

## Remaining legacy contract surfaces

- **Admin modifier-group responses:** ModifierGroupDto.Modifiers still populated by GET /api/modifier-groups, GET /api/modifier-groups/{id}, GET /api/admin/products/{id}/modifier-groups. Required for admin migration and display.
- **Shared DTO:** backend/DTOs/ModifierDTOs.cs ModifierGroupDto.Modifiers remains; POS endpoints return same type with empty list.
- **Cart/order responses:** Cart and table-order item-level SelectedModifiers/modifiers (cart line modifiers) unchanged; separate from modifier-group API.

---

## Follow-up items for PR-D

- **Backend:** If desired, introduce a POS-only DTO (e.g. ModifierGroupForPosDto) without Modifiers property for catalog and Product modifier-groups; would require catalog response type or endpoint contract change. Optional; current approach (empty list) is sufficient for contract slimming.
- **Admin:** When legacy modifier migration is complete and admin no longer needs to display/migrate Modifiers, consider removing Modifiers from admin responses and DTOs in a later phase.
- **Documentation:** Keep Phase D PR-C audit/summary as reference for which endpoints are POS vs admin and which return Modifiers.
