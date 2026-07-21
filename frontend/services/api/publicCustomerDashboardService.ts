import { API_PATHS } from './apiPaths';
import { apiClient } from './config';

export type PublicCustomerOrderSummary = {
  orderNumber: string;
  orderStatus: string;
  total: number;
  currency: string;
  createdAt: string;
};

export type PublicCustomerDashboard = {
  customerDisplayName: string;
  loyaltyPoints: number;
  redeemableEuro: number;
  totalSpent: number;
  totalOrders: number;
  orders: PublicCustomerOrderSummary[];
};

type DashboardApi = {
  customerDisplayName?: string;
  CustomerDisplayName?: string;
  loyaltyPoints?: number;
  LoyaltyPoints?: number;
  redeemableEuro?: number;
  RedeemableEuro?: number;
  totalSpent?: number;
  TotalSpent?: number;
  totalOrders?: number;
  TotalOrders?: number;
  orders?: OrderApi[];
  Orders?: OrderApi[];
};

type OrderApi = {
  orderNumber?: string;
  OrderNumber?: string;
  orderStatus?: string;
  OrderStatus?: string;
  total?: number;
  Total?: number;
  currency?: string;
  Currency?: string;
  createdAt?: string;
  CreatedAt?: string;
};

function mapOrder(dto: OrderApi): PublicCustomerOrderSummary {
  return {
    orderNumber: dto.orderNumber ?? dto.OrderNumber ?? '',
    orderStatus: dto.orderStatus ?? dto.OrderStatus ?? '',
    total: dto.total ?? dto.Total ?? 0,
    currency: dto.currency ?? dto.Currency ?? 'EUR',
    createdAt: dto.createdAt ?? dto.CreatedAt ?? '',
  };
}

function mapDashboard(dto: DashboardApi): PublicCustomerDashboard {
  const orders = (dto.orders ?? dto.Orders ?? []).map(mapOrder);
  return {
    customerDisplayName: dto.customerDisplayName ?? dto.CustomerDisplayName ?? '',
    loyaltyPoints: dto.loyaltyPoints ?? dto.LoyaltyPoints ?? 0,
    redeemableEuro: dto.redeemableEuro ?? dto.RedeemableEuro ?? 0,
    totalSpent: dto.totalSpent ?? dto.TotalSpent ?? 0,
    totalOrders: dto.totalOrders ?? dto.TotalOrders ?? orders.length,
    orders,
  };
}

export async function fetchPublicCustomerDashboard(params: {
  tenant: string;
  phone: string;
}): Promise<PublicCustomerDashboard> {
  const data = await apiClient.get<DashboardApi>(API_PATHS.PUBLIC_CUSTOMER.DASHBOARD, {
    params: {
      tenant: params.tenant.trim(),
      phone: params.phone.trim(),
    },
  });
  return mapDashboard(data ?? {});
}
