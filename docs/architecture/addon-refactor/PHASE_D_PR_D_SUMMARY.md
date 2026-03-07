# Phase D PR-D: Admin Alignment with Add-On-as-Product

**Goal:** Align admin with the new add-on-as-product architecture without breaking migration or historical visibility that is still required.

**Approach:** Slim the admin product modifier-groups endpoint to products-only; keep full modifiers only where migration and legacy display depend on it. Add comments to isolate legacy-only surfaces.

---

## Touched files

### Backend

| File | Change |
|------|--------|
| **backend/Controllers/AdminProductsController.cs** | GetProductModifierGroups: use MapToModifierGroupDtoForAdminProduct (Products populated, Modifiers empty); removed .Include(g => g.Modifiers). Kept MapToModifierGroupDto for any other use. |

### Frontend (admin)

| File | Change |
|------|--------|
| **frontend-admin/src/lib/api/modifierGroups.ts** | Comments: getModifierGroups = full details (legacy modifiers); getProductModifierGroups = products-only, used for assigned IDs; full details from getModifierGroups(). |
| **frontend-admin/src/features/products/components/ExtraZutatenSection.tsx** | Props comment; inline comment that modifiers are legacy-only and come from getModifierGroups(). Subsection label left as "Modifier (Legacy, nur Leseansicht)". |
| **docs/architecture/addon-refactor/PHASE_D_PR_D_SUMMARY.md** | This summary. |

---

## What was modernized

- **GET /api/admin/products/{id}/modifier-groups:** Response now returns modifier groups with **Products** populated and **Modifiers** always empty. Aligns with POS-style products-only contract for “product’s assigned groups.” Admin product form only uses this response for assigned group IDs; it does not use modifiers from this endpoint.
- **Clarified data flow:** getModifierGroups() is the single source for full group data (including modifiers) for both the modifier-groups page and the product form. getProductModifierGroups(productId) is documented as used for assigned group IDs only.

---

## What was intentionally left as legacy-only

- **GET /api/modifier-groups** (ModifierGroupsController): Unchanged. Still returns full ModifierGroupDto including Modifiers. Required for modifier-groups page (list + “Als Produkt migrieren”) and for the group list passed to ExtraZutatenSection (which shows the legacy modifier subsection).
- **GET /api/modifier-groups/{id}:** Unchanged. Still returns full Modifiers for single-group detail if used.
- **modifier-groups/page.tsx:** Unchanged. Still reads g.modifiers and renders legacy list + migrate button (migration-critical).
- **ExtraZutatenSection “Modifier (Legacy, nur Leseansicht)” block:** Left in place. Isolated with comments: data comes from getModifierGroups(), not from the product modifier-groups response. Display remains for historical visibility.

---

## What remains for Phase E

- **Optional removal of legacy display in product form:** If desired, remove the “Modifier (Legacy, nur Leseansicht)” subsection from ExtraZutatenSection so product edit no longer shows the legacy modifier list there; migration would remain only on the modifier-groups page.
- **Post-migration cleanup:** When legacy modifier migration is complete and admin no longer needs to display or migrate Modifiers, consider removing Modifiers from GET /api/modifier-groups (and related DTOs) in a later phase; would require admin UI changes to drop modifier list and migrate flow.
- **Documentation:** Phase D PR-D audit and this summary remain the reference for which endpoints are migration-critical vs products-only.
