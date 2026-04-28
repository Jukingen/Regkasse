# Admin Categories Domain – Deliverable

## What was changed

1. **Stable API boundary**
   - All category flows use `@/api/admin/categories` and the `useCategories()` hook. No legacy category endpoints are used.

2. **List and search flow**
   - Categories page uses two queries in a single flow: **list** when the search term is empty (all categories) and **search** when the user has typed a debounced term (400 ms). Displayed data is `searchQuery.data ?? listQuery.data` when searching, otherwise `listQuery.data`. This matches the backend’s separate `GET /api/admin/categories` and `GET /api/admin/categories/search?query=` endpoints.
   - Search state: local `searchTerm` with debounced `searchDebounced`; server-side search is used when the debounced term is non-empty. `placeholderData: keepPreviousData` keeps the previous result visible while the other query runs.

3. **Unified category flows**
   - Single page: `app/(protected)/categories/page.tsx`. List, create, edit, delete, and category–products view are handled there and in `CategoryForm`, with one API boundary (`@/api/admin/categories` and `useCategories`).
   - Create/update/delete use consistent mutation feedback (`message.success` / `message.error`).

4. **useCategories hook**
   - `invalidateList()` now invalidates `adminCategoriesQueryKeys.all` so all category queries (list and search) refetch after mutations.
   - `useDetail(id)` added for consistency with Products (optional use for a future detail page).
   - `useList(options?)` and `useSearch(query, options?)` accept optional React Query options (e.g. `placeholderData`). Removed redundant `queryKey` overrides; the API module’s keys are used.

5. **Category–products relation view**
   - Expandable row already showed products per category. It is now standardized:
     - Loading: inline `Spin`.
     - Error: `Alert` with message and Retry calling `refetch()`.
     - Empty: `Empty` with description “No products in this category”.
     - Header “Products in this category” above the table for clarity.
   - Still uses `useProductsByCategory(categoryId)` from `useCategories`; data comes from `GET /api/admin/categories/{id}/products`.

6. **Loading, empty, error, mutation feedback**
   - **List**: Table `loading={isLoading}` (list or search depending on term). `locale.emptyText = <Empty description="No categories" />`.
   - **Error**: If the active query (list or search) has `isError`, an `Alert` is shown with message and a Retry button that calls `refetch()`.
   - **Mutations**: Create/update/delete use `message.success` / `message.error`; modal closes on success. Delete button shows per-row loading when that row’s delete is pending.

7. **Alignment with Products domain**
   - Same patterns as Products where safe: single page, error Alert + Retry, explicit Empty state, debounced search, mutations from a single hook, `invalidateList` after mutations. Categories keep list + search as two endpoints (backend does not support a single “list with query” param).

8. **Technical artifacts in English**
   - No new Turkish comments or identifiers. Existing category code was already in English.

---

## Architecture and boundaries

- **API**: Categories domain uses only `@/api/admin/categories` for list, search, create, update, delete, and products-by-category. It does not import `@/api/legacy/*`.
- **Types**: Category types and list params come from `@/api/admin/categories` and generated models where used.
- **Consistency**: Same patterns as the products domain (single page, error Alert + Retry, Empty state, mutations from one hook, invalidateList).

---

## Files modified

| File | Change |
|------|--------|
| `src/features/categories/hooks/useCategories.ts` | JSDoc; `invalidateList` invalidates `adminCategoriesQueryKeys.all`; added `useDetail`; `useList(options?)`, `useSearch(query, options?)`; removed duplicate `queryKey` overrides; export `categoryKeys.search`. |
| `src/app/(protected)/categories/page.tsx` | Debounced search (400 ms) with server-side search when term non-empty; list when empty; error Alert + Retry; Empty state for table; CategoryProducts section with Alert + Retry and “Products in this category” title; `placeholderData: keepPreviousData`; table pagination `showSizeChanger: true`; action buttons `type="text"` for consistency. |
| `docs/ADMIN_CATEGORIES_DOMAIN_DELIVERABLE.md` | New: this deliverable. |

**Not modified (by design)**  
- `src/api/admin/categories.ts` – no changes; already the stable boundary.  
- `src/features/categories/components/CategoryForm.tsx` – no changes.  
- `src/features/categories/components/CategoryList.tsx` – presentational component; not used by the categories page (table is inlined). Kept for potential reuse.  
- `src/features/categories/types.ts` – no changes.  
- Backend – no changes.

---

## Remaining backend contract risks

- **Create/Update payload**: Frontend sends `vatRate` and `sortOrder`; generated `CreateCategoryRequest` / `UpdateCategoryRequest` do not include `vatRate` in the OpenAPI spec. The backend accepts and uses `VatRate`; if the spec is regenerated without it, the frontend may need to keep sending it and document the mismatch.
- **Category list vs search**: Backend returns only **active** categories (`IsActive == true`) for both list and search. Inactive categories are hidden; if admin needs to see or restore inactive ones, the backend would need to expose that.
- **GET category by id**: Returns 404 when category is inactive. Detail view or links to categories must handle 404.
- **Products by category**: `GET /api/admin/categories/{id}/products` returns products; response type is `Product[]`. Any change to response shape (e.g. pagination) would require frontend updates.

---

## Manual QA checklist

- [ ] **List**: Open Categories page; table loads. No search term: list shows all categories (from list endpoint).
- [ ] **Search**: Type in search; after debounce, list filters via search endpoint. Clear search; full list returns.
- [ ] **Create**: Click “New Category”; fill name (and VAT/sort/active); save. New category appears; success message.
- [ ] **Edit**: Click edit on a row; change name/VAT/sort/active; save. Row updates; success message.
- [ ] **Delete**: Click delete; confirm. Row disappears; success message.
- [ ] **Expand products**: Expand a category row; “Products in this category” and product table or empty state appear. Loading shows while fetching; error shows Alert + Retry; Retry refetches.
- [ ] **Error**: Simulate network error (e.g. offline); list or search shows error Alert with Retry. Retry works when back online.
- [ ] **Empty**: Search for a term that matches no categories; table shows “No categories” empty state.
- [ ] **Loading**: During list/search fetch or mutation, loading/confirmLoading is visible; no double submit.

---

## Recommended next step

- **Optional**: Add a category filter on the Products page that uses `useProductFilters()` or local state to set `categoryId` and pass it into products `useList()`, so product list and category choice stay in sync (already supported by the products API).
- **Optional**: If a dedicated category detail page (`/categories/[id]`) is added, use `useCategories().useDetail(id)` and the same admin categories API; keep create/edit in the modal or reuse the same form.
- **Optional**: Regenerate OpenAPI/Orval and add `vatRate` to `CreateCategoryRequest` / `UpdateCategoryRequest` if the backend spec supports it, so frontend and generated types stay aligned.
