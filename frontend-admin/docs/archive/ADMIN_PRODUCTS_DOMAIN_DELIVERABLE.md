# Admin Products Domain – Deliverable

## What was changed

1. **Single list query and filter state**
   - Products page now uses one list query only: `GET /api/admin/products` with params `pageNumber`, `pageSize`, `name` (optional), `categoryId` (optional). The separate search endpoint is no longer used; search is implemented by passing a debounced `name` to the list API.
   - Filter state is simplified: local state for `page`, `pageSize`, `searchTerm`, plus debounced `searchDebounced` (400 ms). Optional `categoryId` can be read from URL via `useProductFilters()` (already supported; UI for category filter can be added later without changing the flow).

2. **Products feature hook (`useProducts`)**
   - `useList(params)` now accepts full `AdminProductsListParams` (pageNumber, pageSize, categoryId, name). Removed `useSearch`; list query covers both listing and name filter.
   - Exposed `useSetModifierGroups` so modifier-group assignment after create/update goes through the same hook and invalidates product cache. Page uses this mutation instead of calling `setProductModifierGroups` from `lib/api/modifierGroups` directly (the lib still delegates to admin API).

3. **Product list/detail/create/update/delete under one structure**
   - Single page: `app/(protected)/products/page.tsx`. All flows (list, create, edit, delete, stock update, modifier-group assignment) are handled there or in `ProductForm` with a single API boundary (`@/api/admin/products` and `useProducts`).
   - Create/update/delete and modifier-group assignment use consistent mutation feedback (message.success / message.error).

4. **Search and filter state**
   - One source of truth for the list: `useList({ pageNumber, pageSize, name?, categoryId? })`. Search input drives `searchTerm` → debounced `searchDebounced` → passed as `name` when length ≥ 2. Pagination is always applied (no separate “search results” mode).

5. **Stock update as explicit action**
   - New “Adjust stock” action in the table: opens a modal with product name and an `InputNumber` for quantity. Save uses `useUpdateAdminProductStock`. Loading (confirmLoading) and success/error messages are shown.

6. **Modifier-group assignment**
   - Unchanged in behavior: still edited in `ProductForm` via `ExtraZutatenSection`. After create/update, the page calls `useSetModifierGroups().mutateAsync` so assignment is part of the same flow and cache is invalidated via admin product keys.

7. **Loading, empty, error, mutation feedback**
   - **Loading**: Table `loading={listQuery.isLoading}`.
   - **Empty**: `locale={{ emptyText: <Empty description="No products" /> }}`.
   - **Error**: If `listQuery.isError`, an `Alert` is shown with message and a “Retry” button that calls `refetch()`.
   - **Mutations**: Create/update/delete/stock/setModifierGroups use `message.success` / `message.error`; modal closes on success where applicable.

8. **English-only technical artifacts**
   - Comments and JSDoc in the products domain and in `productMapper.ts` / `modifierGroups.ts` (product-related parts) are in English.

---

## Architecture and boundaries

- **API**: Products domain uses only `@/api/admin/products` for list, create, update, delete, stock, and modifier-group assignment. It does not import `@/api/legacy/*`.
- **Types**: Product types come from `@/api/generated/model` (Product, etc.); list/params from `@/api/admin/products`.
- **Modifier groups**: Product–modifier-group assignment is done via the admin products API. The modifier-groups list and CRUD live in `src/lib/api/modifierGroups.ts` and call `/api/modifier-groups` (shared endpoint); the page uses `getAdminProductsList` from the admin API for the product list.

---

## Files modified

| File | Change |
|------|--------|
| `src/features/products/hooks/useProducts.ts` | Single `useList(AdminProductsListParams)`; removed `useSearch`; added `useSetModifierGroups`; `invalidateList` only invalidates admin products list; `useProductFilters` type uses string keys for URL. |
| `src/app/(protected)/products/page.tsx` | Single list query with debounced name + categoryId; error Alert + retry; Empty state; stock column + “Adjust stock” modal; create/update use `useSetModifierGroups`; removed direct `setProductModifierGroups` import. |
| `src/features/products/components/ProductForm.tsx` | Comments only: Turkish → English (dropdown options, category, modifier groups load). |
| `src/features/products/components/ExtraZutatenSection.tsx` | JSDoc/props: Turkish → English (selectedGroupIds, onChange, loading, getGroupId). |
| `src/features/products/utils/productMapper.ts` | JSDoc and inline comments translated to English (mapApiProductToUi, mapUiProductToApi, taxType). |
| `src/lib/api/modifierGroups.ts` | Top-level and `setProductModifierGroups` comments: Turkish → English. |
| `docs/ADMIN_PRODUCTS_DOMAIN_DELIVERABLE.md` | New: this deliverable. |

**Not modified (by design)**  
- `src/api/admin/products.ts` – no changes; already the stable boundary.  
- `src/features/products/components/ProductList.tsx` – presentational component; not used by the current products page (table is inlined). Kept for potential reuse.  
- Backend contracts – no changes.

---

## Remaining backend contract risks

- **List vs search**: Backend has both `GET /api/admin/products` (list with name/categoryId) and `GET /api/admin/products/search` (name, category). The app now uses only the list endpoint for listing and filtering. If the backend deprecates or changes the list `name`/`categoryId` semantics, the frontend will need to switch or adapt.
- **Stock update**: `PUT /api/admin/products/stock/{id}` expects `{ quantity: number }`. If the backend adds delta vs absolute semantics or extra fields, the modal and type may need updating.
- **Product form payload**: Create/update send `categoryId` and `category` (name); backend `Product.Category` is required. Any change to required category handling on the backend must be reflected in `mapUiProductToApi` and validation.
- **Modifier-group assignment**: `POST /api/admin/products/{productId}/modifier-groups` with `{ modifierGroupIds: string[] }`. Response and error shape are not typed in the frontend; errors are handled generically.

---

## Manual QA checklist

- [ ] **List**: Open Products page; table loads with pagination. Change page/size; list updates.
- [ ] **Search**: Type at least 2 characters in search; after debounce, list filters by name. Clear search; full list returns.
- [ ] **Create**: Click “New Product”; fill required fields and save. New product appears in list; success message shown.
- [ ] **Edit**: Click edit on a row; change name/price/category and save. Row updates; success message shown.
- [ ] **Delete**: Click delete; confirm. Row disappears; success message shown.
- [ ] **Stock**: Click “Adjust stock” on a row; change quantity and Save. Table stock column updates; success message shown.
- [ ] **Modifier groups**: Create or edit a product; assign add-on groups; save. Re-open edit; assigned groups are pre-selected.
- [ ] **Error**: Simulate network error (e.g. offline); list shows error Alert with Retry. Click Retry when back online.
- [ ] **Empty**: Use a search that matches no products; table shows “No products” empty state.
- [ ] **Loading**: During list fetch or mutation, loading/confirmLoading is visible and buttons are not double-submitted.

---

## Recommended next step

- **Optional**: Add a category filter dropdown on the products page that sets `categoryId` (from `useProductFilters` or local state) and pass it into `useList`, so list and URL stay in sync.
- **Optional**: If a dedicated product detail page (`/products/[id]`) is added later, use `useProducts().useDetail(id)` and the same admin products API; keep create/edit in the modal or move to a shared form used by both page and modal.
