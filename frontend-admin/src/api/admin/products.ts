'use client';

/**
 * Admin products API – all calls use /api/admin/products (legacy api/Product is not used).
 * PUT update: safe parsing workaround when backend returns huge graphs (avoid JSON cycle / network error).
 */
import type {
  UseMutationOptions,
  UseMutationResult,
  UseQueryOptions,
  UseQueryResult,
} from '@tanstack/react-query';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { Product } from '@/api/generated/model';
import { mapUiProductToApi } from '@/features/products/utils/productMapper';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';

const ADMIN_PRODUCTS = '/api/admin/products';

/** Shallow keys only – never store categoryNavigation, modifierGroupAssignments, or any nested graph in cache. */
const SHALLOW_PRODUCT_KEYS = [
  'id',
  'name',
  'nameDe',
  'nameEn',
  'nameTr',
  'price',
  'description',
  'descriptionDe',
  'descriptionEn',
  'descriptionTr',
  'barcode',
  'categoryId',
  'category',
  'taxType',
  'taxRate',
  'isActive',
  'unit',
  'stockQuantity',
  'minStockLevel',
  'cost',
  'imageUrl',
  'createdAt',
  'updatedAt',
  'createdBy',
  'updatedBy',
  'isFiscalCompliant',
  'isTaxable',
  'fiscalCategoryCode',
  'taxExemptionReason',
  'rksvProductType',
] as const;

function pickShallowProduct(obj: unknown): Partial<Product> | null {
  if (obj === null || typeof obj !== 'object') return null;
  const o = obj as Record<string, unknown>;
  const data = (o.data !== undefined ? o.data : o) as Record<string, unknown> | null;
  if (!data || typeof data !== 'object') return null;
  const out: Record<string, unknown> = {};
  for (const key of SHALLOW_PRODUCT_KEYS) {
    const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
    const val = data[key] ?? data[pascalKey];
    if (val !== undefined) out[key] = val;
  }
  return Object.keys(out).length ? (out as Partial<Product>) : null;
}

export type UpdateProductResult = {
  success: true;
  product?: Partial<Product>;
  fromPayload?: boolean;
};

type SecondParameter<T> = T extends (arg: any, arg2?: infer U) => any ? U : never;

/** Omit or 'true': active only (API default). 'false': inactive only. 'all': both. */
export type AdminProductsListIsActiveParam = 'true' | 'false' | 'all';

export type AdminProductsStockStatusParam =
  'InStock' | 'OutOfStock' | 'LowStock' | 'Overstock' | 'All';

export interface AdminProductsListParams {
  pageNumber?: number;
  page?: number;
  pageSize?: number;
  categoryId?: string;
  categoryIds?: string[];
  name?: string;
  searchTerm?: string;
  searchInName?: boolean;
  searchInDescription?: boolean;
  searchInBarcode?: boolean;
  minPrice?: number;
  maxPrice?: number;
  stockStatus?: AdminProductsStockStatusParam;
  minStock?: number;
  maxStock?: number;
  taxTypes?: number[];
  isActive?: AdminProductsListIsActiveParam;
  isTaxable?: boolean;
  createdFrom?: string;
  createdTo?: string;
  sortBy?: string;
  sortDirection?: string;
}

export interface ProductFilterSummaryResponse {
  activeFilterCount?: number;
  appliedFilters?: Record<string, unknown>;
  availableTaxTypes?: number[];
}

export interface ProductAvailableFiltersResponse {
  taxTypes?: number[];
  categories?: Array<{ id: string; name: string }>;
}

export interface AdminProductsListResponse {
  items: Product[];
  pagination: {
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
  activeFilters?: ProductFilterSummaryResponse;
  availableFilters?: ProductAvailableFiltersResponse;
}

function unwrapData<T>(res: any): T {
  if (res?.data !== undefined) return res.data as T;
  return res as T;
}

/** Builds GET query object for list endpoint (stable contract for tests and axios). */
export function buildAdminProductsListQueryParams(params?: AdminProductsListParams) {
  const query: Record<string, unknown> = {
    pageNumber: params?.pageNumber ?? params?.page,
    pageSize: params?.pageSize,
    sortBy: params?.sortBy,
    sortDirection: params?.sortDirection,
    searchTerm: params?.searchTerm ?? params?.name,
    searchInName: params?.searchInName,
    searchInDescription: params?.searchInDescription,
    searchInBarcode: params?.searchInBarcode,
    minPrice: params?.minPrice,
    maxPrice: params?.maxPrice,
    stockStatus: params?.stockStatus,
    minStock: params?.minStock,
    maxStock: params?.maxStock,
    taxTypes: params?.taxTypes,
    isActive: params?.isActive,
    isTaxable: params?.isTaxable,
    createdFrom: params?.createdFrom,
    createdTo: params?.createdTo,
  };

  const categoryIds = params?.categoryIds?.length
    ? params.categoryIds
    : params?.categoryId
      ? [params.categoryId]
      : undefined;
  if (categoryIds?.length) {
    query.categoryIds = categoryIds;
  }

  return query;
}

export function getAdminProductsList(
  params?: AdminProductsListParams,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminProductsListResponse> {
  return customInstance<AdminProductsListResponse>(
    {
      url: ADMIN_PRODUCTS,
      method: 'GET',
      params: buildAdminProductsListQueryParams(params),
      signal,
    },
    options
  ).then((res) => unwrapData(res) as AdminProductsListResponse);
}

export function getAdminProductById(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<Product> {
  return customInstance<Product>(
    { url: `${ADMIN_PRODUCTS}/${id}`, method: 'GET', signal },
    options
  ).then((res) => unwrapData(res) as Product);
}

export function createAdminProduct(
  data: Product,
  options?: SecondParameter<typeof customInstance>
): Promise<{ id?: string } & Product> {
  const payload = prepareAdminProductWritePayload(data);
  return customInstance<{ id?: string } & Product>(
    {
      url: ADMIN_PRODUCTS,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: payload,
    },
    options
  ).then((res) => unwrapData(res) as { id?: string } & Product);
}

/** Normalizes UI/form product into a stable admin write payload (canonical description never null/undefined). */
export function prepareAdminProductWritePayload(
  data: Product & { categoryId?: string; category?: string },
  id?: string
): Record<string, unknown> {
  const payload = mapUiProductToApi(data);
  if (id) payload.id = id;
  else if (data.id) payload.id = data.id;
  if (payload.description === undefined || payload.description === null) {
    payload.description = '';
  }
  if (payload.barcode === undefined || payload.barcode === null) {
    payload.barcode = '';
  }
  return payload;
}

/** Raw PUT – use updateAdminProductSafe for safe parsing and no giant graph in cache. */
export function updateAdminProduct(
  id: string,
  data: Product,
  options?: SecondParameter<typeof customInstance>
) {
  const payload = prepareAdminProductWritePayload(data, id);
  return customInstance<void>(
    {
      url: `${ADMIN_PRODUCTS}/${id}`,
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      data: payload,
    },
    options
  );
}

/**
 * PUT update with safe response handling: 200/204 or parse failure after success.
 * Returns only shallow product fields; never stores categoryNavigation etc. in cache.
 */
export async function updateAdminProductSafe(
  id: string,
  data: Product,
  submittedPayload?: Partial<Product>
): Promise<UpdateProductResult> {
  const payload = prepareAdminProductWritePayload(
    data as Product & { categoryId?: string; category?: string },
    id
  );
  const cachePayload = (submittedPayload ?? payload) as Partial<Product>;
  try {
    const res = await AXIOS_INSTANCE.request({
      url: `${ADMIN_PRODUCTS}/${id}`,
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      data: payload,
    });
    if (res.status === 200 || res.status === 204) {
      const shallow = pickShallowProduct(res.data);
      if (shallow) return { success: true, product: shallow };
      return { success: true, product: cachePayload, fromPayload: true };
    }
    throw new Error(res.status ? `HTTP ${res.status}` : 'Update failed');
  } catch (err: unknown) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 200 || status === 204) {
      return { success: true, product: cachePayload, fromPayload: true };
    }
    throw err;
  }
}

export function deleteAdminProduct(id: string, options?: SecondParameter<typeof customInstance>) {
  return customInstance<void>({ url: `${ADMIN_PRODUCTS}/${id}`, method: 'DELETE' }, options);
}

export type BulkDeactivateAdminProductsResult = {
  deactivated: number;
  alreadyInactive: number;
  notFound: number;
};

export const DEACTIVATE_ALL_PRODUCTS_CONFIRM_PHRASE = 'DEACTIVATE-ALL-PRODUCTS';

export type DeactivateAllAdminProductsResult = {
  deactivated: number;
  alreadyInactive: number;
  totalProducts: number;
};

export function bulkDeactivateAdminProducts(
  productIds: string[],
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<BulkDeactivateAdminProductsResult>(
    {
      url: `${ADMIN_PRODUCTS}/bulk-deactivate`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: { productIds },
    },
    options
  ).then((res) => unwrapData(res) as BulkDeactivateAdminProductsResult);
}

export function deactivateAllAdminProducts(
  confirmPhrase: string = DEACTIVATE_ALL_PRODUCTS_CONFIRM_PHRASE,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<DeactivateAllAdminProductsResult>(
    {
      url: `${ADMIN_PRODUCTS}/deactivate-all`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: { confirmPhrase },
    },
    options
  ).then((res) => unwrapData(res) as DeactivateAllAdminProductsResult);
}

export const DEV_PURGE_CATALOG_CONFIRM_PHRASE = 'DEV-PURGE-CATALOG';
export const DEV_PURGE_CATALOG_FISCAL_OVERRIDE_PHRASE = 'DEV-PURGE-CATALOG-WITH-FISCAL';

export type DevPurgeAdminCatalogResult = {
  tenantId: string;
  productsDeleted: number;
  categoriesDeleted: number;
  hasFiscalPayments: boolean;
};

export type DevPurgeAdminCatalogRequest = {
  tenantSlug?: string;
  tenantId?: string;
  includeCategories?: boolean;
  confirmPhrase: string;
};

export function devPurgeAdminCatalog(
  request: DevPurgeAdminCatalogRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<DevPurgeAdminCatalogResult>(
    {
      url: `${ADMIN_PRODUCTS}/dev/purge-catalog`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: {
        tenantSlug: request.tenantSlug,
        tenantId: request.tenantId,
        includeCategories: request.includeCategories ?? true,
        confirmPhrase: request.confirmPhrase,
      },
    },
    options
  ).then((res) => unwrapData(res) as DevPurgeAdminCatalogResult);
}

export function getAdminProductModifierGroups(
  productId: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
) {
  return customInstance<any>(
    { url: `${ADMIN_PRODUCTS}/${productId}/modifier-groups`, method: 'GET', signal },
    options
  ).then(unwrapData);
}

export function setAdminProductModifierGroups(
  productId: string,
  modifierGroupIds: string[],
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<void>(
    {
      url: `${ADMIN_PRODUCTS}/${productId}/modifier-groups`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: { modifierGroupIds },
    },
    options
  );
}

export function searchAdminProducts(
  params: { name?: string; category?: string },
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<Product[]> {
  return customInstance<Product[]>(
    { url: `${ADMIN_PRODUCTS}/search`, method: 'GET', params, signal },
    options
  ).then((res) => unwrapData(res) as Product[]);
}

export function updateAdminProductStock(
  id: string,
  data: { quantity: number },
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<void>(
    {
      url: `${ADMIN_PRODUCTS}/stock/${id}`,
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      data,
    },
    options
  );
}

const MAX_PRODUCT_IMAGE_BYTES = 2 * 1024 * 1024;

/** Multipart upload — do not use customInstance (JSON unwrap). Returns public image URL for product ImageUrl. */
export async function uploadAdminProductImage(
  productId: string,
  file: File,
  options?: {
    onProgress?: (percent: number) => void;
    signal?: AbortSignal;
  }
): Promise<string> {
  if (file.size > MAX_PRODUCT_IMAGE_BYTES) {
    throw new Error('FILE_TOO_LARGE');
  }
  const formData = new FormData();
  formData.append('file', file);
  const res = await AXIOS_INSTANCE.post<{
    success?: boolean;
    data?: { imageUrl?: string };
    Data?: { ImageUrl?: string };
  }>(`${ADMIN_PRODUCTS}/${productId}/image`, formData, {
    signal: options?.signal,
    onUploadProgress: (ev) => {
      if (ev.total && options?.onProgress) {
        options.onProgress(Math.round((ev.loaded * 100) / ev.total));
      }
    },
  });
  const payload = res.data as { data?: { imageUrl?: string }; Data?: { ImageUrl?: string } };
  const url =
    payload?.data?.imageUrl ?? (payload as { Data?: { ImageUrl?: string } })?.Data?.ImageUrl;
  if (!url || typeof url !== 'string') {
    throw new Error('MISSING_IMAGE_URL');
  }
  return url;
}

export { MAX_PRODUCT_IMAGE_BYTES };

export const adminProductsQueryKeys = {
  all: (tenantSlug: string | null | undefined) => ['admin', 'products', tenantSlug ?? ''] as const,
  lists: (tenantSlug: string | null | undefined) =>
    [...adminProductsQueryKeys.all(tenantSlug), 'list'] as const,
  list: (tenantSlug: string | null | undefined, params?: AdminProductsListParams) =>
    [...adminProductsQueryKeys.lists(tenantSlug), params] as const,
  details: (tenantSlug: string | null | undefined) =>
    [...adminProductsQueryKeys.all(tenantSlug), 'detail'] as const,
  detail: (tenantSlug: string | null | undefined, id: string) =>
    [...adminProductsQueryKeys.details(tenantSlug), id] as const,
  search: (tenantSlug: string | null | undefined, params: { name?: string }) =>
    [...adminProductsQueryKeys.all(tenantSlug), 'search', params] as const,
};

export function useAdminProductsList(
  params?: AdminProductsListParams,
  options?: Partial<UseQueryOptions<AdminProductsListResponse, Error, AdminProductsListResponse>>
): UseQueryResult<AdminProductsListResponse, Error> {
  const { tenantSlug } = useCurrentTenant();
  return useQuery({
    queryKey: adminProductsQueryKeys.list(tenantSlug, params),
    queryFn: ({ signal }) => getAdminProductsList(params, undefined, signal),
    ...options,
  });
}

export function useAdminProductById(
  id: string,
  options?: Partial<UseQueryOptions<Product, Error, Product>>
): UseQueryResult<Product, Error> {
  const { tenantSlug } = useCurrentTenant();
  return useQuery({
    queryKey: adminProductsQueryKeys.detail(tenantSlug, id),
    queryFn: ({ signal }) => getAdminProductById(id, undefined, signal),
    enabled: !!id,
    ...options,
  });
}

export function useAdminProductsSearch(
  params: { name?: string; category?: string },
  options?: Partial<UseQueryOptions<Product[], Error, Product[]>>
): UseQueryResult<Product[], Error> {
  const { tenantSlug } = useCurrentTenant();
  return useQuery({
    queryKey: adminProductsQueryKeys.search(tenantSlug, params),
    queryFn: ({ signal }) => searchAdminProducts(params, undefined, signal),
    enabled: !!(params?.name?.trim() || params?.category?.trim()),
    ...options,
  });
}

export function useCreateAdminProduct(
  opts?: UseMutationOptions<{ id?: string } & Product, Error, { data: Product }>
): UseMutationResult<{ id?: string } & Product, Error, { data: Product }> {
  const qc = useQueryClient();
  const { tenantSlug } = useCurrentTenant();
  return useMutation({
    mutationFn: ({ data }) => createAdminProduct(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists(tenantSlug) }),
    ...opts,
  });
}

/** Build shallow product from payload for cache only (no nested fields). */
function payloadToShallow(p: Product & { categoryId?: string }): Partial<Product> {
  const out: Record<string, unknown> = {};
  const record = p as unknown as Record<string, unknown>;
  for (const key of SHALLOW_PRODUCT_KEYS) {
    const v = record[key];
    if (v !== undefined) out[key] = v;
  }
  return out as Partial<Product>;
}

export function useUpdateAdminProduct(
  opts?: UseMutationOptions<UpdateProductResult, Error, { id: string; data: Product }>
): UseMutationResult<UpdateProductResult, Error, { id: string; data: Product }> {
  const qc = useQueryClient();
  const { tenantSlug } = useCurrentTenant();
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminProductSafe(id, data, payloadToShallow(data)),
    onSuccess: (result, { id, data }) => {
      const shallow = result.product ?? payloadToShallow(data);
      const safeProduct = { ...shallow, id } as Product;
      qc.setQueryData(adminProductsQueryKeys.detail(tenantSlug, id), safeProduct);
      qc.setQueriesData(
        { queryKey: adminProductsQueryKeys.lists(tenantSlug) },
        (old: AdminProductsListResponse | undefined) => {
          if (!old?.items) return old;
          return {
            ...old,
            items: old.items.map((item) => (item.id === id ? { ...item, ...safeProduct } : item)),
          };
        }
      );
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.detail(tenantSlug, id) });
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists(tenantSlug) });
    },
    ...opts,
  });
}

export function useDeleteAdminProduct(
  opts?: UseMutationOptions<void, Error, { id: string }>
): UseMutationResult<void, Error, { id: string }> {
  const qc = useQueryClient();
  const { tenantSlug } = useCurrentTenant();
  return useMutation({
    mutationFn: ({ id }) => deleteAdminProduct(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists(tenantSlug) }),
    ...opts,
  });
}

export function useBulkDeactivateAdminProducts(
  opts?: UseMutationOptions<BulkDeactivateAdminProductsResult, Error, { productIds: string[] }>
): UseMutationResult<BulkDeactivateAdminProductsResult, Error, { productIds: string[] }> {
  const qc = useQueryClient();
  const { tenantSlug } = useCurrentTenant();
  return useMutation({
    mutationFn: ({ productIds }) => bulkDeactivateAdminProducts(productIds),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists(tenantSlug) }),
    ...opts,
  });
}

export function useDeactivateAllAdminProducts(
  opts?: UseMutationOptions<DeactivateAllAdminProductsResult, Error, void>
): UseMutationResult<DeactivateAllAdminProductsResult, Error, void> {
  const qc = useQueryClient();
  const { tenantSlug } = useCurrentTenant();
  return useMutation({
    mutationFn: () => deactivateAllAdminProducts(),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists(tenantSlug) }),
    ...opts,
  });
}

export function useDevPurgeAdminCatalog(
  opts?: UseMutationOptions<DevPurgeAdminCatalogResult, Error, DevPurgeAdminCatalogRequest>
): UseMutationResult<DevPurgeAdminCatalogResult, Error, DevPurgeAdminCatalogRequest> {
  const qc = useQueryClient();
  const { tenantSlug } = useCurrentTenant();
  return useMutation({
    mutationFn: (request) => devPurgeAdminCatalog(request),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists(tenantSlug) }),
    ...opts,
  });
}

export function useSetAdminProductModifierGroups(
  opts?: UseMutationOptions<void, Error, { productId: string; modifierGroupIds: string[] }>
): UseMutationResult<void, Error, { productId: string; modifierGroupIds: string[] }> {
  const qc = useQueryClient();
  const { tenantSlug } = useCurrentTenant();
  return useMutation({
    mutationFn: ({ productId, modifierGroupIds }) =>
      setAdminProductModifierGroups(productId, modifierGroupIds),
    onSuccess: (_, { productId }) =>
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.detail(tenantSlug, productId) }),
    ...opts,
  });
}

export function useUpdateAdminProductStock(
  opts?: UseMutationOptions<void, Error, { id: string; data: { quantity: number } }>
): UseMutationResult<void, Error, { id: string; data: { quantity: number } }> {
  const qc = useQueryClient();
  const { tenantSlug } = useCurrentTenant();
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminProductStock(id, data),
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.detail(tenantSlug, id) });
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists(tenantSlug) });
    },
    ...opts,
  });
}

export type DemoProductImportResult = {
  success: boolean;
  created: number;
  updated: number;
  skipped: number;
  selectedCategoryCount?: number;
  totalProductCount?: number;
  categoriesCreated?: number;
  importedProductCount?: number;
  averageImportedPrice?: number;
  categorySummaries?: Array<{
    categoryName: string;
    productCount: number;
    created: number;
    skipped: number;
  }>;
  errorMessage?: string | null;
  categoryIds?: string[];
  productIds?: string[];
};

export type DemoImportRequest = {
  overwriteExisting?: boolean;
  selectedCategories?: string[];
  excludedCategories?: string[];
  selectedProductIds?: string[];
  /** none | increasePercent | decreasePercent | roundUpToIncrement */
  priceAdjustmentMode?: string;
  priceAdjustmentPercent?: number;
  priceRoundIncrement?: number;
  /** none | categoryPlaceholder | defaultAsset */
  imageMode?: string;
  productOverrides?: DemoImportProductOverride[];
};

export type DemoImportProductOverride = {
  catalogProductId: string;
  price?: number;
  taxRate?: number;
};

export type DemoImportCatalogCategory = {
  name: string;
  description?: string | null;
  sortOrder: number;
  productCount: number;
  vatRate: number;
};

export type DemoImportCatalog = {
  categories: DemoImportCatalogCategory[];
  products: Array<{
    id: string;
    name: string;
    category: string;
    price: number;
    taxRate: number;
  }>;
};

/** Demo menu catalog for import selection UI. */
export function getDemoImportCatalog(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<DemoImportCatalog> {
  return customInstance<DemoImportCatalog>(
    { url: `${ADMIN_PRODUCTS}/demo/catalog`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<DemoImportCatalog>(res));
}

/** Import demo menu for the current tenant (JWT / dev tenant context). */
export function importDemoProducts(
  request: DemoImportRequest = {},
  options?: SecondParameter<typeof customInstance>
): Promise<DemoProductImportResult> {
  return customInstance<DemoProductImportResult>(
    {
      url: `${ADMIN_PRODUCTS}/demo/import`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    options
  ).then((res) => unwrapData<DemoProductImportResult>(res));
}
