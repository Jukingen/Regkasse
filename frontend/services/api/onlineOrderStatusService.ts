import { API_PATHS } from './apiPaths';
import { apiClient } from './config';

export type PublicOnlineOrderStatus = {
  orderNumber: string;
  orderStatus: string;
  orderType: string;
  total: number;
  currency: string;
  createdAt: string;
  acceptedAt?: string | null;
  readyAt?: string | null;
  completedAt?: string | null;
  customerDisplayName?: string | null;
};

type StatusApi = {
  orderNumber?: string;
  OrderNumber?: string;
  orderStatus?: string;
  OrderStatus?: string;
  orderType?: string;
  OrderType?: string;
  total?: number;
  Total?: number;
  currency?: string;
  Currency?: string;
  createdAt?: string;
  CreatedAt?: string;
  acceptedAt?: string | null;
  AcceptedAt?: string | null;
  readyAt?: string | null;
  ReadyAt?: string | null;
  completedAt?: string | null;
  CompletedAt?: string | null;
  customerDisplayName?: string | null;
  CustomerDisplayName?: string | null;
};

function mapStatus(dto: StatusApi): PublicOnlineOrderStatus {
  return {
    orderNumber: dto.orderNumber ?? dto.OrderNumber ?? '',
    orderStatus: dto.orderStatus ?? dto.OrderStatus ?? '',
    orderType: dto.orderType ?? dto.OrderType ?? '',
    total: dto.total ?? dto.Total ?? 0,
    currency: dto.currency ?? dto.Currency ?? 'EUR',
    createdAt: dto.createdAt ?? dto.CreatedAt ?? '',
    acceptedAt: dto.acceptedAt ?? dto.AcceptedAt,
    readyAt: dto.readyAt ?? dto.ReadyAt,
    completedAt: dto.completedAt ?? dto.CompletedAt,
    customerDisplayName: dto.customerDisplayName ?? dto.CustomerDisplayName,
  };
}

export async function fetchPublicOnlineOrderStatus(params: {
  tenant: string;
  orderNumber: string;
  phone?: string;
}): Promise<PublicOnlineOrderStatus> {
  const data = await apiClient.get<StatusApi>(API_PATHS.PUBLIC_ONLINE_ORDERS.STATUS, {
    params: {
      tenant: params.tenant.trim(),
      orderNumber: params.orderNumber.trim(),
      phone: params.phone?.trim() || undefined,
    },
  });
  return mapStatus(data ?? {});
}
