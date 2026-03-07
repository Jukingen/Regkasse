/**
 * Admin products API – all calls use /api/admin/products (legacy api/Product is not used).
 * PUT update: safe parsing workaround when backend returns huge graphs (avoid JSON cycle / network error).
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { UseMutationOptions, UseQueryOptions, UseQueryResult, UseMutationResult } from '@tanstack/react-query';
import { customInstance, AXIOS_INSTANCE } from '@/lib/axios';
import type { Product } from '@/api/generated/model';

const ADMIN_PRODUCTS = '/api/admin/products';

/** Shallow keys only – never store categoryNavigation, modifierGroupAssignments, or any nested graph in cache. */
const SHALLOW_PRODUCT_KEYS = [
  'id', 'name', 'price', 'description', 'barcode', 'categoryId', 'category', 'taxType', 'taxRate',
  'isActive', 'unit', 'stockQuantity', 'minStockLevel', 'cost', 'imageUrl',
  'createdAt', 'updatedAt', 'createdBy', 'updatedBy',
  'isFiscalCompliant', 'isTaxable', 'fiscalCategoryCode', 'taxExemptionReason', 'rksvProductType',
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

export type UpdateProductResult = { success: true; product?: Partial<Product>; fromPayload?: boolean };

type SecondParameter<T> = T extends (arg: any, arg2?: infer U) => any ? U : never;

export interface AdminProductsListParams {
  pageNumber?: number;
  pageSize?: number;
  categoryId?: string;
  name?: string;
}

export interface AdminProductsListResponse {
  items: Product[];
  pagination: {
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
}

function unwrapData<T>(res: any): T {
  if (res?.data !== undefined) return res.data as T;
  return res as T;
}

export function getAdminProductsList(
  params?: AdminProductsListParams,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminProductsListResponse> {
  return customInstance<AdminProductsListResponse>(
    { url: ADMIN_PRODUCTS, method: 'GET', params: { pageNumber: params?.pageNumber, pageSize: params?.pageSize, categoryId: params?.categoryId, name: params?.name }, signal },
    options
  ).then((res) => unwrapData(res) as AdminProductsListResponse);
}

export function getAdminProductById(id: string, options?: SecondParameter<typeof customInstance>, signal?: AbortSignal): Promise<Product> {
  return customInstance<Product>({ url: `${ADMIN_PRODUCTS}/${id}`, method: 'GET', signal }, options).then((res) => unwrapData(res) as Product);
}

export function createAdminProduct(data: Product, options?: SecondParameter<typeof customInstance>): Promise<{ id?: string } & Product> {
  return customInstance<{ id?: string } & Product>({ url: ADMIN_PRODUCTS, method: 'POST', headers: { 'Content-Type': 'application/json' }, data }, options).then((res) => unwrapData(res) as { id?: string } & Product);
}

/** Raw PUT – use updateAdminProductSafe for safe parsing and no giant graph in cache. */
export function updateAdminProduct(id: string, data: Product, options?: SecondParameter<typeof customInstance>) {
  return customInstance<void>({ url: `${ADMIN_PRODUCTS}/${id}`, method: 'PUT', headers: { 'Content-Type': 'application/json' }, data }, options);
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
  try {
    const res = await AXIOS_INSTANCE.request({
      url: `${ADMIN_PRODUCTS}/${id}`,
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      data,
    });
    if (res.status === 200 || res.status === 204) {
      const shallow = pickShallowProduct(res.data);
      if (shallow) return { success: true, product: shallow };
      return { success: true, product: submittedPayload ?? undefined, fromPayload: true };
    }
    throw new Error(res.status ? `HTTP ${res.status}` : 'Update failed');
  } catch (err: any) {
    const status = err?.response?.status;
    if (status === 200 || status === 204) {
      return { success: true, product: submittedPayload ?? undefined, fromPayload: true };
    }
    throw err;
  }
}

export function deleteAdminProduct(id: string, options?: SecondParameter<typeof customInstance>) {
  return customInstance<void>({ url: `${ADMIN_PRODUCTS}/${id}`, method: 'DELETE' }, options);
}

export function getAdminProductModifierGroups(productId: string, options?: SecondParameter<typeof customInstance>, signal?: AbortSignal) {
  return customInstance<any>({ url: `${ADMIN_PRODUCTS}/${productId}/modifier-groups`, method: 'GET', signal }, options).then(unwrapData);
}

export function setAdminProductModifierGroups(productId: string, modifierGroupIds: string[], options?: SecondParameter<typeof customInstance>) {
  return customInstance<void>(
    { url: `${ADMIN_PRODUCTS}/${productId}/modifier-groups`, method: 'POST', headers: { 'Content-Type': 'application/json' }, data: { modifierGroupIds } },
    options
  );
}

export function searchAdminProducts(
  params: { name?: string; category?: string },
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<Product[]> {
  return customInstance<Product[]>({ url: `${ADMIN_PRODUCTS}/search`, method: 'GET', params, signal }, options).then((res) => unwrapData(res) as Product[]);
}

export function updateAdminProductStock(
  id: string,
  data: { quantity: number },
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<void>({ url: `${ADMIN_PRODUCTS}/stock/${id}`, method: 'PUT', headers: { 'Content-Type': 'application/json' }, data }, options);
}

export const adminProductsQueryKeys = {
  all: ['admin', 'products'] as const,
  lists: () => [...adminProductsQueryKeys.all, 'list'] as const,
  list: (params?: AdminProductsListParams) => [...adminProductsQueryKeys.lists(), params] as const,
  details: () => [...adminProductsQueryKeys.all, 'detail'] as const,
  detail: (id: string) => [...adminProductsQueryKeys.details(), id] as const,
  search: (params: { name?: string }) => [...adminProductsQueryKeys.all, 'search', params] as const,
};

export function useAdminProductsList(
  params?: AdminProductsListParams,
  options?: Partial<UseQueryOptions<AdminProductsListResponse, Error, AdminProductsListResponse>>
): UseQueryResult<AdminProductsListResponse, Error> {
  return useQuery({
    queryKey: adminProductsQueryKeys.list(params),
    queryFn: ({ signal }) => getAdminProductsList(params, undefined, signal),
    ...options,
  });
}

export function useAdminProductById(
  id: string,
  options?: Partial<UseQueryOptions<Product, Error, Product>>
): UseQueryResult<Product, Error> {
  return useQuery({
    queryKey: adminProductsQueryKeys.detail(id),
    queryFn: ({ signal }) => getAdminProductById(id, undefined, signal),
    enabled: !!id,
    ...options,
  });
}

export function useAdminProductsSearch(
  params: { name?: string; category?: string },
  options?: Partial<UseQueryOptions<Product[], Error, Product[]>>
): UseQueryResult<Product[], Error> {
  return useQuery({
    queryKey: adminProductsQueryKeys.search(params),
    queryFn: ({ signal }) => searchAdminProducts(params, undefined, signal),
    enabled: !!(params?.name?.trim() || params?.category?.trim()),
    ...options,
  });
}

export function useCreateAdminProduct(
  opts?: UseMutationOptions<{ id?: string } & Product, Error, { data: Product }>
): UseMutationResult<{ id?: string } & Product, Error, { data: Product }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ data }) => createAdminProduct(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists() }),
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
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminProductSafe(id, data, payloadToShallow(data)),
    onSuccess: (result, { id, data }) => {
      const shallow = result.product ?? payloadToShallow(data);
      const safeProduct = { ...shallow, id } as Product;
      qc.setQueryData(adminProductsQueryKeys.detail(id), safeProduct);
      qc.setQueriesData(
        { queryKey: adminProductsQueryKeys.lists() },
        (old: AdminProductsListResponse | undefined) => {
          if (!old?.items) return old;
          return {
            ...old,
            items: old.items.map((item) => (item.id === id ? { ...item, ...safeProduct } : item)),
          };
        }
      );
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.detail(id) });
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists() });
    },
    ...opts,
  });
}

export function useDeleteAdminProduct(opts?: UseMutationOptions<void, Error, { id: string }>): UseMutationResult<void, Error, { id: string }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id }) => deleteAdminProduct(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists() }),
    ...opts,
  });
}

export function useSetAdminProductModifierGroups(
  opts?: UseMutationOptions<void, Error, { productId: string; modifierGroupIds: string[] }>
): UseMutationResult<void, Error, { productId: string; modifierGroupIds: string[] }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, modifierGroupIds }) => setAdminProductModifierGroups(productId, modifierGroupIds),
    onSuccess: (_, { productId }) => qc.invalidateQueries({ queryKey: adminProductsQueryKeys.detail(productId) }),
    ...opts,
  });
}

export function useUpdateAdminProductStock(
  opts?: UseMutationOptions<void, Error, { id: string; data: { quantity: number } }>
): UseMutationResult<void, Error, { id: string; data: { quantity: number } }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminProductStock(id, data),
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.detail(id) });
      qc.invalidateQueries({ queryKey: adminProductsQueryKeys.lists() });
    },
    ...opts,
  });
}
