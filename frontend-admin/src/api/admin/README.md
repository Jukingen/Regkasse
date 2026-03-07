# Admin API Boundary (Stable)

This folder is the **stable** API boundary for admin-only domains. All calls use `/api/admin/*`; legacy paths (`/api/Product`, `/api/Categories`) are not used here.

## Modules

- **products.ts** – `/api/admin/products`: list, getById, search, create, update, delete, stock, modifier-groups. Query keys: `adminProductsQueryKeys`. Use hooks from this file (e.g. `useAdminProductsList`, `useAdminProductById`) or the feature hooks that wrap them (`@/features/products/hooks/useProducts`).
- **categories.ts** – `/api/admin/categories`: list, getById, create, update, delete, products-by-category, search. Query keys: `adminCategoriesQueryKeys`. Use hooks from this file or `@/features/categories/hooks/useCategories`.

## Rules

- New admin product/category features must use this module only. Do not add new direct calls to legacy Product or Categories endpoints.
- Typed client: `customInstance` from `@/lib/axios`. Query keys and mutations are defined in each module for cache invalidation and refetch boundaries.
