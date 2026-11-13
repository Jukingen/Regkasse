# FE-Admin: Admin route migration

**Date:** 2025-03-05  
**Goal:** Move product and category calls to `/api/admin/*`. Do not call legacy endpoints directly anywhere in FE-Admin.

---

## 1) Changed files

| File | Change |
|------|--------|
| `orval.config.ts` | Comment: admin product/category uses `src/api/admin/*`. |
| `src/api/admin/products.ts` | All calls `/api/admin/products`: list, getById, search, create, update, delete, stock, modifier-groups (legacy product endpoint removed). |
| `src/api/admin/categories.ts` | **New.** All category calls `/api/admin/categories`. |
| `src/features/products/hooks/useProducts.ts` | Uses `@/api/admin/products` instead of generated `product/product`. |
| `src/features/categories/hooks/useCategories.ts` | Uses `@/api/admin/categories` instead of generated `categories/categories`. |
| `src/app/(protected)/products/page.tsx` | List/search shape updated for admin response (`listQuery.data?.items`, `listQuery.data?.pagination`); create result `result?.id`. |
| `src/lib/api/modifierGroups.ts` | `getProductModifierGroups` and `setProductModifierGroups` now go through `@/api/admin/products`. |
| `src/features/products/components/ProductForm.tsx` | Comment: categories use admin. |

---

## 2) Orval config diff

```diff
 import { defineConfig } from 'orval';

+/**
+ * Orval: Backend swagger.json → generated clients (tags-split).
+ * Admin product/category usage: use src/api/admin/* instead of generated
+ * (GET /api/admin/products, GET /api/admin/categories, etc.).
+ */
 export default defineConfig({
     kasse: {
         input: {
```

Orval input/target/mutator stayed the same; only the admin mapping comment was added. New endpoints were written by hand under `src/api/admin/*` until swagger emits admin-tagged paths.

---

## 3) Critical hook/component conversions

### useProducts (before / after)

**Before** (`src/features/products/hooks/useProducts.ts`): generated `useGetApiProduct*` from `@/api/generated/product/product`.

**After:** admin hooks from `@/api/admin/products` (`useAdminProductsList`, `useAdminProductsSearch`, `useAdminProductById`, `useCreateAdminProduct`, `useUpdateAdminProduct`, `useDeleteAdminProduct`, `useUpdateAdminProductStock`). Query keys stay aligned with `adminProductsQueryKeys`.

### useCategories (before / after)

**Before:** generated `useGetApiCategories*` from `@/api/generated/categories/categories`.

**After:** `@/api/admin/categories` (`useAdminCategoriesList`, `useAdminCategoriesSearch`, `useAdminCategoryProducts`, `useCreateAdminCategory`, `useDeleteAdminCategory`, `useUpdateAdminCategory`).

### Products page – list/search/pagination

**Before:** nested `data.data.items` / `data.data.pagination`.

**After:**

```ts
const rawSearchResults = Array.isArray(searchQuery.data) ? searchQuery.data : [];
const rawListItems = listQuery.data?.items ?? [];
const pagination = listQuery.data?.pagination
  ? { current: page, total: listQuery.data.pagination.totalCount, ... }
  : false;
```

### Products page – create result

**Before:** `result?.data?.id`

**After:** `result?.id`

### modifierGroups

**Before:** legacy product modifier-groups endpoints on `AXIOS_INSTANCE`.

**After:** `getAdminProductModifierGroups` / `setAdminProductModifierGroups` from `@/api/admin/products`.

---

## 4) Endpoint mapping (what FE-Admin calls now)

| Operation | Old (no longer used) | New (FE-Admin) |
|-----------|----------------------|----------------|
| Product list | GET legacy product (or /list) | GET /api/admin/products |
| Product detail | GET legacy product/{id} | GET /api/admin/products/{id} |
| Product search | GET legacy product/search | Admin client → `src/api/admin/products` search |
| Product create/update/delete, stock, modifier-groups | legacy product/* | Admin client → `src/api/admin/products` |
| Category list/detail/CRUD/search/products-by-category | legacy categories/* | GET/POST/PUT/DELETE /api/admin/categories, /api/admin/categories/{id}, /search, /{id}/products |
| Modifier groups (assign/get on product) | legacy product modifier-groups | `modifierGroups.ts` → `@/api/admin/products` |

UI behavior (list load, edit, create, delete) is unchanged; only the data source moved to admin routes (and the admin client instead of legacy).
