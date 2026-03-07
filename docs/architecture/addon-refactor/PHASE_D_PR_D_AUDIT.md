# Phase D PR-D: Admin Dependency on Legacy Modifier Fields — Audit

**Goal:** Audit admin dependency on legacy modifier fields and prepare a minimal alignment plan. No code changes in this audit.

**Context:** POS is products-only; POS write path and POS-facing modifier-group contract are already slimmed. Admin may still depend on legacy modifier visibility for migration and historical display.

---

## 1. Where Admin Still Reads ModifierGroupDto.Modifiers

| File | Location | What is read | Data source |
|------|----------|--------------|-------------|
| **frontend-admin/src/app/(protected)/modifier-groups/page.tsx** | Lines 209, 264–294 | `g.modifiers` per group | `getModifierGroups()` → GET /api/modifier-groups |
| **frontend-admin/src/features/products/components/ExtraZutatenSection.tsx** | Lines 53, 83–94 | `group.modifiers` per group | `groups` prop from ProductForm → `getModifierGroups()` → GET /api/modifier-groups |
| **frontend-admin/src/lib/api/modifierGroups.ts** | Lines 28–37, 49–50, 56–58 | Type `ModifierGroupDto.modifiers`; API responses | getModifierGroups (GET /api/modifier-groups), getProductModifierGroups (GET /api/admin/products/{id}/modifier-groups) |

**Data flow clarification:** ProductForm loads (1) `getModifierGroups()` for the full list of groups and (2) `getProductModifierGroups(productId)` only to get **assigned group IDs** for the product. It sets `modifierGroups = allGroups` (from getModifierGroups) and passes that to ExtraZutatenSection. So **all modifier display in admin** (both modifier-groups page and product form) ultimately comes from **GET /api/modifier-groups** only. GET /api/admin/products/{id}/modifier-groups is used only to populate `selectedModifierGroupIds`; its response body (including any modifiers) is not used for rendering group details.

---

## 2. Usage Classification

| Usage | File | Classification | Reason |
|-------|------|----------------|--------|
| List legacy modifiers per group + "Als Produkt migrieren" button | modifier-groups/page.tsx | **Migration-critical** | Admins must see which legacy modifiers exist and trigger POST .../modifiers/{id}/migrate. Removing modifiers from this flow would break migration. |
| "Modifier (Legacy, nur Leseansicht)" subsection per group | ExtraZutatenSection.tsx | **Historical display only** | Read-only list (name, price). No migrate action. Could be removed or replaced with a count/summary if we stop sending modifiers for the data source that feeds this component. |
| ModifierGroupDto.modifiers type and API client | modifierGroups.ts | **Contract** | Required for migration UI and for ExtraZutatenSection as long as they display modifiers. |

**Per-endpoint:**

- **GET /api/modifier-groups:** **Migration-critical.** Sole source of full group list (with modifiers) for (1) modifier-groups page (list + migration) and (2) ProductForm/ExtraZutatenSection (groups prop). Must keep returning Modifiers.
- **GET /api/modifier-groups/{id}:** Not used by current admin UI (no direct call found). If used later for single-group detail, would need modifiers for migration consistency. Keep as-is.
- **GET /api/admin/products/{id}/modifier-groups:** Used only to get assigned group IDs in ProductForm. **Replaceable:** response could omit modifiers (products-only or id/name only); UI would be unchanged because group details (and modifiers) come from getModifierGroups().

---

## 3. Removable vs Replaceable

| Item | Removable? | Replaceable? | Notes |
|------|------------|--------------|--------|
| modifier-groups page: legacy modifier list + migrate button | No | No | Core migration flow. |
| ExtraZutatenSection: "Modifier (Legacy, nur Leseansicht)" block | Yes | Yes | Display only; could remove subsection or show "N Legacy-Modifier" if backend added a count. Removal is minimal. |
| Modifiers in GET /api/modifier-groups response | No | No | Required for migration and for current ExtraZutatenSection (which gets groups from this endpoint). |
| Modifiers in GET /api/admin/products/{id}/modifier-groups response | Yes (for contract slimming) | N/A | Not used for display; only IDs are used. Backend could return groups without modifiers for this endpoint only. |

---

## 4. Does Admin Need a Dedicated Legacy Endpoint/Surface?

**No.** Existing admin surfaces already serve as the legacy surface:

- **GET /api/modifier-groups** and **GET /api/modifier-groups/{id}** (ModifierGroupsController) are admin-only in practice and return full ModifierGroupDto including Modifiers. They are the right place for migration and legacy display.
- A separate "legacy modifiers" endpoint would duplicate data and add surface area. Keeping modifiers on the existing modifier-groups endpoints is sufficient.

**Optional alignment:** The only endpoint that could be slimmed without changing admin behavior is **GET /api/admin/products/{id}/modifier-groups**: return modifier groups with products only (no Modifiers), since ProductForm only uses that response for assigned group IDs. That would be a contract alignment with POS-style "groups without modifiers" for this one admin product endpoint; no new endpoint required.

---

## 5. Blockers

| Blocker | Impact |
|--------|--------|
| Removing Modifiers from GET /api/modifier-groups | Would break modifier-groups page (no list of legacy modifiers, no migrate button) and ExtraZutatenSection (modifier list comes from getModifierGroups()). |
| Removing Modifiers from GET /api/modifier-groups/{id} | Would break any future or existing single-group detail view that shows legacy modifiers or migration. |
| Removing migrateLegacyModifier (POST .../modifiers/{id}/migrate) or modifier list from modifier-groups page | Would remove ability to migrate legacy modifiers to products. |

**No blocker** to slimming GET /api/admin/products/{id}/modifier-groups (return groups without modifiers), because no admin code uses modifiers from that response.

---

## 6. Minimal Safe Admin Refactor Plan (PR-D)

**Option A – No backend change (minimal):**

- **Backend:** No changes. All admin endpoints keep returning Modifiers as today.
- **Admin frontend:** Optional doc/comment only: clarify that modifier-groups page and ExtraZutatenSection depend on GET /api/modifier-groups for modifiers; GET /api/admin/products/{id}/modifier-groups is used only for assigned group IDs.
- **Outcome:** No risk; admin continues to work. No contract slimming.

**Option B – Slim one admin endpoint (alignment):**

- **Backend:** In AdminProductsController, for GET /api/admin/products/{id}/modifier-groups only, use a mapper that returns modifier groups with **Products** populated and **Modifiers = empty** (same shape as POS ProductController for product modifier-groups). ModifierGroupsController unchanged (still returns full Modifiers).
- **Admin frontend:** No change. ProductForm and ExtraZutatenSection get full groups (with modifiers) from getModifierGroups(); getProductModifierGroups(productId) is only used for IDs.
- **Outcome:** One fewer endpoint returns modifiers; admin behavior unchanged. Clear split: "modifier-groups" endpoints = full legacy; "admin product modifier-groups" = products-only like POS.

**Option C – Remove historical-only display (optional):**

- **Admin frontend:** In ExtraZutatenSection, remove the "Modifier (Legacy, nur Leseansicht)" subsection (lines 83–94). Groups would still come from getModifierGroups() (with modifiers); we would simply not render them in the product form. Migration would remain on modifier-groups page only.
- **Backend:** No change required. Optionally combine with Option B so that GET /api/admin/products/{id}/modifier-groups returns no modifiers and no admin code expects modifiers from that response.
- **Outcome:** Less UI surface for legacy modifiers on product edit; migration and full list stay on modifier-groups page.

**Recommended for PR-D:** **Option B** (slim GET /api/admin/products/{id}/modifier-groups only). Option A is acceptable if the goal is zero change. Option C can follow in a later PR if desired.

---

## File list (admin modifier usage)

| File | Role |
|------|------|
| frontend-admin/src/app/(protected)/modifier-groups/page.tsx | Reads g.modifiers; renders list + migrate button; migration-critical. |
| frontend-admin/src/features/products/components/ExtraZutatenSection.tsx | Reads group.modifiers; renders "Legacy, nur Leseansicht" list; historical display only. |
| frontend-admin/src/features/products/components/ProductForm.tsx | Loads getModifierGroups + getProductModifierGroups; passes allGroups to ExtraZutatenSection; no direct read of modifiers. |
| frontend-admin/src/lib/api/modifierGroups.ts | Defines ModifierGroupDto.modifiers; getModifierGroups, getProductModifierGroups; migrateLegacyModifier. |
| frontend-admin/src/api/admin/products.ts | getAdminProductModifierGroups(productId) → GET /api/admin/products/{id}/modifier-groups. |
| backend/Controllers/ModifierGroupsController.cs | GET /api/modifier-groups, GET /api/modifier-groups/{id}; returns full Modifiers. |
| backend/Controllers/AdminProductsController.cs | GET /api/admin/products/{id}/modifier-groups; currently returns full Modifiers (optional slim target). |

---

## Recommended PR-D implementation plan

1. **Document** (no code change): Add a short note in docs or in modifierGroups.ts that admin modifier visibility and migration depend on GET /api/modifier-groups; GET /api/admin/products/{id}/modifier-groups is used only for assigned group IDs.
2. **Optional – Backend:** In AdminProductsController, add MapToModifierGroupDtoForPos (or equivalent) for GetProductModifierGroups only: return ModifierGroupDto with Products and Modifiers = empty. Leave ModifierGroupsController unchanged.
3. **Optional – Frontend:** If Option B is done, no admin frontend change (ProductForm does not use modifiers from getProductModifierGroups). If Option C is desired later, remove the "Modifier (Legacy, nur Leseansicht)" block from ExtraZutatenSection.
4. **Do not:** Remove or slim Modifiers from GET /api/modifier-groups or GET /api/modifier-groups/{id}; do not remove migration UI or migrateLegacyModifier.
