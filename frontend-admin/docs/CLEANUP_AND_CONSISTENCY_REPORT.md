# Admin Frontend – Cleanup and Consistency Report

Final cleanup and architecture consistency review (no new features). All technical artifacts remain in English; no backend contract or flow changes.

---

## 1. Cleanup summary

- **Turkish technical comments** replaced with English in:
  - `src/features/products/utils/productMapper.ts` (JSDoc and inline comments for mapApiProductToUi, mapUiProductToApi, taxType)
  - `src/lib/api/modifierGroups.ts` (file header and JSDoc for interfaces and functions)
  - `src/shared/auth/AdminOnlyGate.tsx` (component JSDoc)
  - `src/theme/index.ts` (theme colors and locale comment)
- **Documentation**: Short “Architecture and boundaries” sections added to:
  - `docs/ADMIN_PRODUCTS_DOMAIN_DELIVERABLE.md`
  - `docs/ADMIN_CATEGORIES_DOMAIN_DELIVERABLE.md`
  - `docs/LEGACY_MODIFIER_MIGRATION_DELIVERABLE.md`
- **Products deliverable**: Updated to state that productMapper and modifierGroups comments are now English; removed the follow-up item about normalizing Turkish in productMapper.

No functional changes, no new features, no backend or API contract changes.

---

## 2. Files modified

| File | Change |
|------|--------|
| `src/features/products/utils/productMapper.ts` | Turkish JSDoc and inline comments → English. |
| `src/lib/api/modifierGroups.ts` | Top comment and JSDoc (AddOnGroupProductItemDto, createModifierGroup, updateModifierGroup, addModifierToGroup deprecation, AddProductToGroupBody) → English. |
| `src/shared/auth/AdminOnlyGate.tsx` | Component JSDoc → English. |
| `src/theme/index.ts` | “Tema renkleri”, “Mavi”, “Kırmızı”, “Almanca yerelleştirme” → English. |
| `docs/ADMIN_PRODUCTS_DOMAIN_DELIVERABLE.md` | Added “Architecture and boundaries”; updated “English-only technical artifacts” and productMapper row; removed Turkish follow-up from recommended next steps. |
| `docs/ADMIN_CATEGORIES_DOMAIN_DELIVERABLE.md` | Added “Architecture and boundaries”. |
| `docs/LEGACY_MODIFIER_MIGRATION_DELIVERABLE.md` | Added “Architecture and boundaries”. |
| `docs/CLEANUP_AND_CONSISTENCY_REPORT.md` | **New.** This report. |

---

## 3. Architecture observations

- **Admin catalog vs legacy**
  - **Products**: Uses only `@/api/admin/products` and `@/api/generated/model` (types). No imports from `@/api/legacy/*`.
  - **Categories**: Uses only `@/api/admin/categories`. No legacy imports.
  - **Payments**: Uses only `@/api/legacy/payment` (by design; legacy payments).
  - **Modifier-groups page**: Uses `getAdminProductsList` from `@/api/admin/products`; migration from `@/lib/api/legacyModifierMigration` and `@/lib/api/modifierGroups`. ModifierGroups lib uses `@/api/admin/products` for product modifier-group assignment. No direct legacy API imports for catalog or migration.
- **Query keys**: Products and categories use `adminProductsQueryKeys` and `adminCategoriesQueryKeys` from their API modules. Modifier-groups page uses local keys (`modifierGroupsKey`, `adminProductsListKey`, `migrationProgressKey`). Pattern is consistent; no change made.
- **Generated clients**: Admin modules use `src/api/admin/*`; generated clients are used via those modules or for types from `@/api/generated/model`. No ad-hoc direct imports from generated hooks in random places.

---

## 4. Remaining technical debt

- **Theme/localization**: `src/theme/index.ts` is shared; remaining strings are in English. Any future theme or locale additions should stay English for technical artifacts.
- **Modifier-groups query keys**: Keys are local to the page. If modifier-groups get a dedicated feature module later, consider a shared key factory (e.g. `adminModifierGroupsQueryKeys`) for consistency with products/categories.
- **Product type cast**: `productMapper.ts` still uses `taxType as unknown as string` because the generated `Product` type expects `taxType: string` while the backend uses int; payload sends number. Documented in comment; type alignment would require backend/OpenAPI or generated type change.
- **Category vatRate**: Categories deliverable already notes that `vatRate` may be missing from generated `CreateCategoryRequest`/`UpdateCategoryRequest`; frontend sends it and backend accepts it. Regenerating OpenAPI and aligning types would reduce mismatch risk.
- **Commented import**: `src/app/(protected)/layout.tsx` still has a commented-out import (`// import { usePostApiAuthLogout } from '@/api/generated/auth/auth'; // Replaced by useAuth`). Low priority; can be removed in a small cleanup if desired.

---

## 5. Potential API contract mismatches

- **Success envelope**: Admin endpoints may return `{ success, message, data }`. Migration module and any other defensive unwrappers assume this; if an endpoint returns payload at root, code uses `res.data?.data ?? res.data`. New endpoints with a different envelope may need unwrap logic updated.
- **List vs search**: Products use only the list endpoint with `name`/`categoryId`; categories use separate list and search. Backend semantics for list params and search should stay consistent with current usage.
- **Modifier-group assignment**: Response/error shape for `POST /api/admin/products/{productId}/modifier-groups` is not typed in the frontend; errors are handled generically. Adding a typed response/error would improve robustness.

---

## 6. Areas that may require backend changes later

- **Category vatRate**: If OpenAPI spec is updated to include `vatRate` on create/update category, frontend can rely on generated types; until then, sending `vatRate` is intentional and documented.
- **Product taxType**: Backend uses int (1,2,3,4); generated `Product` has `taxType: string`. Backend or codegen alignment would allow removing the cast in productMapper.
- **Bulk migration result**: If backend adds or renames fields on the migration result DTO, frontend types and result UI would need to be updated.
- **Inactive categories**: Backend returns only active categories for list/search; if admin needs to see or restore inactive categories, backend would need to expose that.

---

## 7. Low-risk future improvements

- Remove the commented-out auth import in `(protected)/layout.tsx` if no longer needed.
- Add a shared query key factory for modifier-groups if the domain grows (e.g. dedicated hooks or multiple consumers).
- Optionally type the modifier-group assignment API response/errors.
- Add a category filter dropdown on the products page (already supported by API and `useProductFilters`).

---

## 8. Recommended next steps

- **None required.** Cleanup and consistency goals are met; architecture boundaries are documented.
- **Optional**: Apply the low-risk improvements above when touching the relevant areas.
- **Optional**: When regenerating OpenAPI, align category request types and product taxType if the backend spec supports it.
