import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';
import { queryCacheDynamic } from '@/lib/query/queryCachePolicy';

export interface ProductVariant {
  id: string;
  productId: string;
  sku: string;
  variantLabel: string;
  priceDelta: number;
  stock: number;
  isActive: boolean;
}

export interface ProductVariantWriteBody {
  sku: string;
  variantLabel: string;
  priceDelta: number;
  stock: number;
  isActive?: boolean;
}

export const adminProductVariantQueryKeys = {
  byProduct: (productId: string) => ['admin', 'product-variants', productId] as const,
};

export function getAdminProductVariants(productId: string, signal?: AbortSignal): Promise<ProductVariant[]> {
  return customInstance<ProductVariant[]>(
    { url: `/api/admin/products/${productId}/variants`, method: 'GET', signal }
  ).then((res) => unwrapData<ProductVariant[]>(res));
}

export function createAdminProductVariant(
  productId: string,
  body: ProductVariantWriteBody
): Promise<ProductVariant> {
  return customInstance<ProductVariant>(
    { url: `/api/admin/products/${productId}/variants`, method: 'POST', data: body }
  ).then((res) => unwrapData<ProductVariant>(res));
}

export function updateAdminProductVariant(
  productId: string,
  variantId: string,
  body: ProductVariantWriteBody
): Promise<ProductVariant> {
  return customInstance<ProductVariant>(
    {
      url: `/api/admin/products/${productId}/variants/${variantId}`,
      method: 'PUT',
      data: body,
    }
  ).then((res) => unwrapData<ProductVariant>(res));
}

export function deactivateAdminProductVariant(
  productId: string,
  variantId: string
): Promise<ProductVariant> {
  return customInstance<ProductVariant>(
    { url: `/api/admin/products/${productId}/variants/${variantId}`, method: 'DELETE' }
  ).then((res) => unwrapData<ProductVariant>(res));
}

export function useAdminProductVariants(
  productId: string | undefined,
  enabled: boolean,
  options?: Partial<UseQueryOptions<ProductVariant[], Error, ProductVariant[]>>
): UseQueryResult<ProductVariant[], Error> {
  return useQuery({
    queryKey: adminProductVariantQueryKeys.byProduct(productId ?? ''),
    enabled: enabled && Boolean(productId),
    queryFn: ({ signal }) => getAdminProductVariants(productId ?? '', signal),
    ...queryCacheDynamic,
    ...options,
  });
}
