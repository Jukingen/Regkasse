/**
 * Admin categories API – all calls use /api/admin/categories.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { UseMutationOptions, UseQueryOptions, UseQueryResult, UseMutationResult } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';
import type { Category, CreateCategoryRequest, UpdateCategoryRequest, Product } from '@/api/generated/model';

const ADMIN_CATEGORIES = '/api/admin/categories';

type SecondParameter<T> = T extends (arg: any, arg2?: infer U) => any ? U : never;

function unwrapData<T>(res: any): T {
  if (res?.data !== undefined) return res.data as T;
  return res as T;
}

export function getAdminCategories(options?: SecondParameter<typeof customInstance>, signal?: AbortSignal) {
  return customInstance<Category[]>({ url: ADMIN_CATEGORIES, method: 'GET', signal }, options).then(unwrapData);
}

export function getAdminCategoryById(id: string, options?: SecondParameter<typeof customInstance>, signal?: AbortSignal) {
  return customInstance<Category>({ url: `${ADMIN_CATEGORIES}/${id}`, method: 'GET', signal }, options).then(unwrapData);
}

export function createAdminCategory(data: CreateCategoryRequest, options?: SecondParameter<typeof customInstance>) {
  return customInstance<Category>({ url: ADMIN_CATEGORIES, method: 'POST', headers: { 'Content-Type': 'application/json' }, data }, options).then(unwrapData);
}

export function updateAdminCategory(id: string, data: UpdateCategoryRequest, options?: SecondParameter<typeof customInstance>) {
  return customInstance<void>({ url: `${ADMIN_CATEGORIES}/${id}`, method: 'PUT', headers: { 'Content-Type': 'application/json' }, data }, options);
}

export function deleteAdminCategory(id: string, options?: SecondParameter<typeof customInstance>) {
  return customInstance<void>({ url: `${ADMIN_CATEGORIES}/${id}`, method: 'DELETE' }, options);
}

export function getAdminCategoryProducts(id: string, options?: SecondParameter<typeof customInstance>, signal?: AbortSignal) {
  return customInstance<Product[]>({ url: `${ADMIN_CATEGORIES}/${id}/products`, method: 'GET', signal }, options).then(unwrapData);
}

export function searchAdminCategories(query: string, options?: SecondParameter<typeof customInstance>, signal?: AbortSignal) {
  return customInstance<Category[]>({ url: `${ADMIN_CATEGORIES}/search`, method: 'GET', params: { query }, signal }, options).then(unwrapData);
}

export const adminCategoriesQueryKeys = {
  all: ['admin', 'categories'] as const,
  lists: () => [...adminCategoriesQueryKeys.all, 'list'] as const,
  details: () => [...adminCategoriesQueryKeys.all, 'detail'] as const,
  detail: (id: string) => [...adminCategoriesQueryKeys.details(), id] as const,
  products: (id: string) => [...adminCategoriesQueryKeys.detail(id), 'products'] as const,
  search: (query: string) => [...adminCategoriesQueryKeys.all, 'search', query] as const,
};

export function useAdminCategoriesList(
  options?: Partial<UseQueryOptions<Category[], Error, Category[]>>
): UseQueryResult<Category[], Error> {
  return useQuery({
    queryKey: adminCategoriesQueryKeys.lists(),
    queryFn: ({ signal }) => getAdminCategories(undefined, signal),
    ...options,
  });
}

export function useAdminCategoryById(
  id: string,
  options?: Partial<UseQueryOptions<Category, Error, Category>>
): UseQueryResult<Category, Error> {
  return useQuery({
    queryKey: adminCategoriesQueryKeys.detail(id),
    queryFn: ({ signal }) => getAdminCategoryById(id, undefined, signal),
    enabled: !!id,
    ...options,
  });
}

export function useAdminCategoryProducts(
  id: string,
  options?: Partial<UseQueryOptions<Product[], Error, Product[]>>
): UseQueryResult<Product[], Error> {
  return useQuery({
    queryKey: adminCategoriesQueryKeys.products(id),
    queryFn: ({ signal }) => getAdminCategoryProducts(id, undefined, signal),
    enabled: !!id,
    ...options,
  });
}

export function useAdminCategoriesSearch(
  query: string,
  options?: Partial<UseQueryOptions<Category[], Error, Category[]>>
): UseQueryResult<Category[], Error> {
  return useQuery({
    queryKey: adminCategoriesQueryKeys.search(query),
    queryFn: ({ signal }) => searchAdminCategories(query, undefined, signal),
    enabled: !!query.trim(),
    ...options,
  });
}

export function useCreateAdminCategory(
  opts?: UseMutationOptions<Category, Error, { data: CreateCategoryRequest }>
): UseMutationResult<Category, Error, { data: CreateCategoryRequest }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ data }) => createAdminCategory(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminCategoriesQueryKeys.lists() }),
    ...opts,
  });
}

export function useUpdateAdminCategory(
  opts?: UseMutationOptions<void, Error, { id: string; data: UpdateCategoryRequest }>
): UseMutationResult<void, Error, { id: string; data: UpdateCategoryRequest }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminCategory(id, data),
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: adminCategoriesQueryKeys.detail(id) });
      qc.invalidateQueries({ queryKey: adminCategoriesQueryKeys.lists() });
    },
    ...opts,
  });
}

export function useDeleteAdminCategory(opts?: UseMutationOptions<void, Error, { id: string }>): UseMutationResult<void, Error, { id: string }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id }) => deleteAdminCategory(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminCategoriesQueryKeys.lists() }),
    ...opts,
  });
}
