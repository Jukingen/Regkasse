'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  acceptOnlineOrder,
  fetchOnlineOrderAnalytics,
  fetchOnlineOrderById,
  fetchOnlineOrders,
  updateOnlineOrderStatus,
} from '@/features/orders/api/onlineOrdersApi';

export const ONLINE_ORDERS_QUERY_KEY = ['admin', 'online-orders'] as const;

/** Forward kitchen step for status-only online orders (no POS). */
export function getNextOnlineOrderStatus(current: string): string | null {
  switch (current.toLowerCase()) {
    case 'pending':
      return 'accepted';
    case 'accepted':
      return 'preparing';
    case 'preparing':
      return 'ready';
    case 'ready':
      return 'completed';
    default:
      return null;
  }
}

export function useOrders(status?: string) {
  return useQuery({
    queryKey: [...ONLINE_ORDERS_QUERY_KEY, status ?? 'all'],
    queryFn: () => fetchOnlineOrders(status),
    refetchInterval: 15_000,
  });
}

export function useOnlineOrderAnalytics() {
  return useQuery({
    queryKey: [...ONLINE_ORDERS_QUERY_KEY, 'analytics'],
    queryFn: fetchOnlineOrderAnalytics,
    refetchInterval: 60_000,
  });
}

export function useOnlineOrderDetail(id: string | null) {
  return useQuery({
    queryKey: [...ONLINE_ORDERS_QUERY_KEY, 'detail', id],
    queryFn: () => fetchOnlineOrderById(id!),
    enabled: !!id,
  });
}

/** @deprecated Prefer status-only {@link useUpdateOnlineOrderStatus} — Accept pushes into POS. */
export function useAcceptOnlineOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => acceptOnlineOrder(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ONLINE_ORDERS_QUERY_KEY });
    },
  });
}

export function useUpdateOnlineOrderStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) =>
      updateOnlineOrderStatus(id, status),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ONLINE_ORDERS_QUERY_KEY });
    },
  });
}
