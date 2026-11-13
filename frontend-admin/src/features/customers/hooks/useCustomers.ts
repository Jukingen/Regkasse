import { useQueryClient } from '@tanstack/react-query';

import {
  useDeleteApiCustomerId,
  useGetApiCustomer,
  useGetApiCustomerCount,
  useGetApiCustomerId,
  useGetApiCustomerSearch,
  usePostApiCustomer,
  usePutApiCustomerId,
} from '@/api/generated/customer/customer';
import { Customer } from '@/api/generated/model';
import { useURLFilters } from '@/hooks/useURLFilters';

// Backend returns { success, message, data, timestamp }. List data = { items, pagination }; search data = Customer[].
function isObjectWithData(raw: unknown): raw is { data: unknown } {
  return typeof raw === 'object' && raw !== null && 'data' in raw;
}

/** Extract items from GET /api/Customer response (paginated list envelope). */
export function extractListItems(raw: unknown): Customer[] {
  if (!isObjectWithData(raw)) return [];
  const data = raw.data as { items?: unknown };
  if (data && typeof data === 'object' && 'items' in data && Array.isArray(data.items))
    return data.items as Customer[];
  return [];
}

/** Extract items from GET /api/Customer/search response (array envelope). */
export function extractSearchItems(raw: unknown): Customer[] {
  if (!isObjectWithData(raw)) return [];
  if (Array.isArray(raw.data)) return raw.data as Customer[];
  return [];
}

export type ListParams = { page?: number; pageSize?: number; search?: string };

// 1. Key Factory
export const customerKeys = {
  all: ['customers'] as const,
  lists: () => [...customerKeys.all, 'list'] as const,
  list: (filters: string) => [...customerKeys.lists(), { filters }] as const,
  details: () => [...customerKeys.all, 'detail'] as const,
  detail: (id: string) => [...customerKeys.details(), id] as const,
  count: () => [...customerKeys.all, 'count'] as const,
};

// 2. Filter Hook
export function useCustomerFilters() {
  return useURLFilters<{
    page: number;
    pageSize: number;
    search: string;
  }>();
}

// 3. Main Hook
export function useCustomers() {
  const queryClient = useQueryClient();

  const invalidateList = () => {
    queryClient.invalidateQueries({ queryKey: customerKeys.lists() });
    queryClient.invalidateQueries({ queryKey: customerKeys.count() });
    queryClient.invalidateQueries({ queryKey: ['/api/Customer'] });
  };

  return {
    /** List or search: when search is set uses GET /api/Customer/search?name=; otherwise GET /api/Customer with pagination. Returns unwrapped Customer[]. */
    useList: (params?: ListParams) => {
      const hasSearch = Boolean(params?.search?.trim());
      const listParams =
        !hasSearch && (params?.page != null || params?.pageSize != null)
          ? { pageNumber: Number(params?.page) || 1, pageSize: Number(params?.pageSize) || 10 }
          : undefined;
      const searchParams = hasSearch ? { name: params?.search?.trim() ?? '' } : undefined;
      const queryKey = customerKeys.list(JSON.stringify(params ?? {}));

      const listQuery = useGetApiCustomer(listParams, {
        query: {
          queryKey,
          enabled: !hasSearch,
          select: extractListItems,
        },
      });
      const searchQuery = useGetApiCustomerSearch(searchParams, {
        query: {
          queryKey,
          enabled: hasSearch,
          select: extractSearchItems,
        },
      });

      return hasSearch
        ? { ...searchQuery, data: searchQuery.data ?? [] }
        : { ...listQuery, data: listQuery.data ?? [] };
    },
    useSearch: useGetApiCustomerSearch,
    useDetail: useGetApiCustomerId,
    useCount: useGetApiCustomerCount,

    // Mutations
    useCreate: usePostApiCustomer,
    useUpdate: usePutApiCustomerId,
    useDelete: useDeleteApiCustomerId,

    // Utils
    invalidateList,
    keys: customerKeys,
  };
}
