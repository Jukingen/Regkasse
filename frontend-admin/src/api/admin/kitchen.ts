import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export type KitchenAdminOrderStatus =
  | 'Pending'
  | 'InPreparation'
  | 'Ready'
  | 'Served'
  | 'Cancelled';

export interface KitchenSettings {
  autoClearMinutes: number;
  soundEnabled: boolean;
}

export interface KitchenAdminOrderItem {
  id: string;
  productName: string;
  quantity: number;
  notes?: string | null;
  status: string;
}

export interface KitchenAdminOrder {
  id: string;
  tableNumber?: string | null;
  status: KitchenAdminOrderStatus | string;
  notes?: string | null;
  createdAtUtc: string;
  items: KitchenAdminOrderItem[];
}

export interface KitchenAnalytics {
  averagePrepMinutes: number | null;
  ordersPerHour: number;
  createdLast24Hours: number;
  createdLastHour: number;
}

export type KitchenApiScope = { tenantId?: string };

function kitchenBase(scope?: KitchenApiScope): string {
  return scope?.tenantId
    ? `/api/admin/tenants/${encodeURIComponent(scope.tenantId)}/kitchen`
    : '/api/admin/kitchen';
}

export const kitchenQueryKeys = {
  all: ['admin', 'kitchen'] as const,
  settings: (tenantId?: string) => [...kitchenQueryKeys.all, 'settings', tenantId ?? 'ambient'] as const,
  orders: (tenantId?: string) => [...kitchenQueryKeys.all, 'orders', tenantId ?? 'ambient'] as const,
  analytics: (tenantId?: string) =>
    [...kitchenQueryKeys.all, 'analytics', tenantId ?? 'ambient'] as const,
};

export function getKitchenSettings(
  scope?: KitchenApiScope,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<KitchenSettings> {
  return customInstance<KitchenSettings>(
    { url: `${kitchenBase(scope)}/settings`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<KitchenSettings>(res));
}

export function updateKitchenSettings(
  body: KitchenSettings,
  scope?: KitchenApiScope,
  options?: SecondParameter<typeof customInstance>
): Promise<KitchenSettings> {
  return customInstance<KitchenSettings>(
    { url: `${kitchenBase(scope)}/settings`, method: 'PATCH', data: body },
    options
  ).then((res) => unwrapData<KitchenSettings>(res));
}

export function listKitchenOrders(
  scope?: KitchenApiScope,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<KitchenAdminOrder[]> {
  return customInstance<KitchenAdminOrder[]>(
    { url: `${kitchenBase(scope)}/orders`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<KitchenAdminOrder[]>(res));
}

export function getKitchenAnalytics(
  scope?: KitchenApiScope,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<KitchenAnalytics> {
  return customInstance<KitchenAnalytics>(
    { url: `${kitchenBase(scope)}/analytics`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<KitchenAnalytics>(res));
}

export function useKitchenSettings(
  scope?: KitchenApiScope,
  options?: Partial<UseQueryOptions<KitchenSettings, Error, KitchenSettings>>
): UseQueryResult<KitchenSettings, Error> {
  return useQuery({
    queryKey: kitchenQueryKeys.settings(scope?.tenantId),
    queryFn: ({ signal }) => getKitchenSettings(scope, undefined, signal),
    ...options,
  });
}

export function useKitchenOrders(
  scope?: KitchenApiScope,
  options?: Partial<UseQueryOptions<KitchenAdminOrder[], Error, KitchenAdminOrder[]>>
): UseQueryResult<KitchenAdminOrder[], Error> {
  return useQuery({
    queryKey: kitchenQueryKeys.orders(scope?.tenantId),
    queryFn: ({ signal }) => listKitchenOrders(scope, undefined, signal),
    refetchInterval: 5000,
    ...options,
  });
}

export function useKitchenAnalytics(
  scope?: KitchenApiScope,
  options?: Partial<UseQueryOptions<KitchenAnalytics, Error, KitchenAnalytics>>
): UseQueryResult<KitchenAnalytics, Error> {
  return useQuery({
    queryKey: kitchenQueryKeys.analytics(scope?.tenantId),
    queryFn: ({ signal }) => getKitchenAnalytics(scope, undefined, signal),
    refetchInterval: 30_000,
    ...options,
  });
}
