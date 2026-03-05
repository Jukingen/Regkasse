# FE-Admin: Admin route migrasyonu

**Tarih:** 2025-03-05  
**Hedef:** Product ve category çağrılarını `/api/admin/*` kullanacak şekilde taşımak; eski endpoint’leri FE-Admin içinde hiçbir yerde doğrudan çağırmamak.

---

## 1) Değişen dosyalar listesi

| Dosya | Değişiklik |
|-------|------------|
| `orval.config.ts` | Açıklama eklendi: admin product/category için `src/api/admin/*` kullanıldığı belirtildi. |
| `src/api/admin/products.ts` | Tüm çağrılar `/api/admin/products`: list, getById, search, create, update, delete, stock, modifier-groups (legacy product endpoint kaldırıldı). |
| `src/api/admin/categories.ts` | **Yeni.** Tüm kategori çağrıları `/api/admin/categories`. |
| `src/features/products/hooks/useProducts.ts` | Generated `product/product` yerine `@/api/admin/products` kullanıyor. |
| `src/features/categories/hooks/useCategories.ts` | Generated `categories/categories` yerine `@/api/admin/categories` kullanıyor. |
| `src/app/(protected)/products/page.tsx` | List/search veri şekli admin response’a göre güncellendi (`listQuery.data?.items`, `listQuery.data?.pagination`); create sonucu `result?.id`. |
| `src/lib/api/modifierGroups.ts` | `getProductModifierGroups` ve `setProductModifierGroups` artık `@/api/admin/products` üzerinden. |
| `src/features/products/components/ProductForm.tsx` | Yorum güncellendi: categories için admin kullanıldığı yazıldı. |

---

## 2) Orval config diff

```diff
 import { defineConfig } from 'orval';

+/**
+ * Orval: Backend swagger.json → generated clients (tags-split).
+ * Admin product/category kullanımı: generated yerine src/api/admin/* kullanılır
+ * (GET /api/admin/products, GET /api/admin/categories vb.).
+ */
 export default defineConfig({
     kasse: {
         input: {
```

Orval input/target/mutator aynı kaldı; sadece admin mapping açıklaması eklendi. Yeni endpoint’ler için kod `src/api/admin/*` içinde manuel yazıldı (swagger’da admin tag’li path’ler üretilene kadar).

---

## 3) Kritik hook/component dönüşümleri

### useProducts (önce / sonra)

**Önce** (`src/features/products/hooks/useProducts.ts`):

```ts
import {
    useGetApiProduct,
    useGetApiProductSearch,
    useGetApiProductId,
    usePostApiProduct,
    usePutApiProductId,
    useDeleteApiProductId,
    usePutApiProductIdStock
} from '@/api/generated/product/product';
// ...
return {
    useList: (params, options) => useGetApiProduct({ pageNumber: params?.page, pageSize: params?.pageSize }, { query: { queryKey: productKeys.list(...), ...options?.query } }),
    useSearch: useGetApiProductSearch,
    useDetail: useGetApiProductId,
    useCreate: usePostApiProduct,
    useUpdate: usePutApiProductId,
    useDelete: useDeleteApiProductId,
    useUpdateStock: usePutApiProductIdStock,
    invalidateList: () => { queryClient.invalidateQueries({ queryKey: productKeys.lists() }); ... },
    keys: productKeys,
};
```

**Sonra**:

```ts
import {
    useAdminProductsList,
    useAdminProductsSearch,
    useAdminProductById,
    useCreateAdminProduct,
    useUpdateAdminProduct,
    useDeleteAdminProduct,
    useUpdateAdminProductStock,
    adminProductsQueryKeys,
} from '@/api/admin/products';
// ...
return {
    useList: (params, options) => useAdminProductsList({ pageNumber: params?.page, pageSize: params?.pageSize }, { queryKey: productKeys.list(...), ...options?.query }),
    useSearch: (params, options) => useAdminProductsSearch({ name: params?.name, category: params?.category }, { enabled: options?.query?.enabled ?? !!params?.name }),
    useDetail: (id, options) => useAdminProductById(id, options?.query),
    useCreate: useCreateAdminProduct,
    useUpdate: useUpdateAdminProduct,
    useDelete: useDeleteAdminProduct,
    useUpdateStock: useUpdateAdminProductStock,
    invalidateList: () => { queryClient.invalidateQueries({ queryKey: adminProductsQueryKeys.lists() }); ... },
    keys: productKeys, // productKeys.all = adminProductsQueryKeys.all
};
```

### useCategories (önce / sonra)

**Önce**:

```ts
import {
    useGetApiCategories,
    usePostApiCategories,
    usePutApiCategoriesId,
    useDeleteApiCategoriesId,
    useGetApiCategoriesIdProducts,
    useGetApiCategoriesSearch
} from '@/api/generated/categories/categories';
// useList: () => useGetApiCategories({ query: { queryKey: categoryKeys.lists() } }),
// useSearch: (query) => useGetApiCategoriesSearch({ query }, { query: { queryKey: [...], enabled: !!query } }),
// ...
```

**Sonra**:

```ts
import {
    useAdminCategoriesList,
    useAdminCategoriesSearch,
    useAdminCategoryProducts,
    useCreateAdminCategory,
    useUpdateAdminCategory,
    useDeleteAdminCategory,
    adminCategoriesQueryKeys,
} from '@/api/admin/categories';
// useList: () => useAdminCategoriesList({ queryKey: categoryKeys.lists() }),
// useSearch: (query) => useAdminCategoriesSearch(query, { queryKey: [...], enabled: !!query }),
// useProductsByCategory: (id) => useAdminCategoryProducts(id, { queryKey: categoryKeys.products(id), enabled: !!id }),
```

### Products page – list/search/pagination (önce / sonra)

**Önce**:

```ts
const rawSearchResults = (searchQuery.data as any)?.data?.data ?? (searchQuery.data as any)?.data ?? [];
const rawListItems = listQuery.data?.data?.items || [];
// ...
const pagination = listQuery.data?.data?.pagination ? { current: page, total: listQuery.data.data.pagination.totalCount, ... } : false;
```

**Sonra**:

```ts
const rawSearchResults = Array.isArray(searchQuery.data) ? searchQuery.data : [];
const rawListItems = listQuery.data?.items ?? [];
// ...
const pagination = listQuery.data?.pagination ? { current: page, total: listQuery.data.pagination.totalCount, ... } : false;
```

### Products page – create result (önce / sonra)

**Önce**:

```ts
const result = await createMutation.mutateAsync({ data: apiData }) as { data?: { id?: string } };
const createdId = result?.data?.id;
```

**Sonra**:

```ts
const result = await createMutation.mutateAsync({ data: apiData }) as { id?: string };
const createdId = result?.id;
```

### modifierGroups (önce / sonra)

**Önce**:

```ts
// Önce: legacy product modifier-groups endpoint'leri
export async function getProductModifierGroups(productId: string) {
  const res = await AXIOS_INSTANCE.get(`.../modifier-groups`); // legacy path
  // ...
}
export async function setProductModifierGroups(productId: string, modifierGroupIds: string[]) {
  await AXIOS_INSTANCE.post(`.../modifier-groups`, { modifierGroupIds });
}
```

**Sonra**:

```ts
import { getAdminProductModifierGroups, setAdminProductModifierGroups as setAdminProductModifierGroupsApi } from '@/api/admin/products';
// getProductModifierGroups → getAdminProductModifierGroups(productId)
// setProductModifierGroups → setAdminProductModifierGroupsApi(productId, modifierGroupIds)
```

---

## 4) Endpoint eşlemesi (FE-Admin artık ne çağırıyor)

| İşlem | Eski (artık kullanılmıyor) | Yeni (FE-Admin) |
|-------|----------------------------|------------------|
| Ürün listesi | GET legacy product (veya /list) | GET /api/admin/products |
| Ürün detay | GET legacy product/{id} | GET /api/admin/products/{id} |
| Ürün arama | GET legacy product/search | Admin client → `src/api/admin/products` search |
| Ürün oluştur/güncelle/sil, stok, modifier-groups | legacy product/* | Admin client → `src/api/admin/products` |
| Kategori listesi/detay/CRUD/search/products-by-category | legacy categories/* | GET/POST/PUT/DELETE /api/admin/categories, /api/admin/categories/{id}, /search, /{id}/products |
| Modifier grupları (ürüne ata/getir) | legacy product modifier-groups | `modifierGroups.ts` → `@/api/admin/products` (getAdminProductModifierGroups, setAdminProductModifierGroups) |

UI davranışı (liste yükleme, edit, create, delete) aynı kaldı; yalnızca veri kaynağı admin route’larına (ve admin client üzerinden legacy’e) taşındı.
