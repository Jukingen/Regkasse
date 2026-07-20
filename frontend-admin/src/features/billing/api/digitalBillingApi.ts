import { customInstance } from '@/lib/axios';

export type DigitalBillingSubscriptionRow = {
  id: string;
  tenantId: string;
  tenant: string;
  service: string;
  serviceId: string;
  price: number;
  currency: string;
  startDate: string;
  nextBilling: string;
  status: string;
};

export type DigitalBillingDashboard = {
  total: number;
  websites: number;
  apps: number;
  subscribers: number;
  currency: string;
  subscriptions: DigitalBillingSubscriptionRow[];
};

type RowApi = {
  id?: string;
  Id?: string;
  tenantId?: string;
  TenantId?: string;
  tenant?: string;
  Tenant?: string;
  service?: string;
  Service?: string;
  serviceId?: string;
  ServiceId?: string;
  price?: number;
  Price?: number;
  currency?: string;
  Currency?: string;
  startDate?: string;
  StartDate?: string;
  nextBilling?: string;
  NextBilling?: string;
  status?: string;
  Status?: string;
};

type DashboardApi = {
  total?: number;
  Total?: number;
  websites?: number;
  Websites?: number;
  apps?: number;
  Apps?: number;
  subscribers?: number;
  Subscribers?: number;
  currency?: string;
  Currency?: string;
  subscriptions?: RowApi[];
  Subscriptions?: RowApi[];
};

function mapRow(dto: RowApi): DigitalBillingSubscriptionRow {
  return {
    id: dto.id ?? dto.Id ?? '',
    tenantId: dto.tenantId ?? dto.TenantId ?? '',
    tenant: dto.tenant ?? dto.Tenant ?? '',
    service: dto.service ?? dto.Service ?? '',
    serviceId: dto.serviceId ?? dto.ServiceId ?? '',
    price: dto.price ?? dto.Price ?? 0,
    currency: dto.currency ?? dto.Currency ?? 'EUR',
    startDate: dto.startDate ?? dto.StartDate ?? '',
    nextBilling: dto.nextBilling ?? dto.NextBilling ?? '',
    status: dto.status ?? dto.Status ?? '',
  };
}

export async function fetchDigitalBillingDashboard(): Promise<DigitalBillingDashboard> {
  const res = await customInstance<DashboardApi>({
    url: '/api/admin/billing/digital',
    method: 'GET',
  });
  const rows = res?.subscriptions ?? res?.Subscriptions ?? [];
  return {
    total: res?.total ?? res?.Total ?? 0,
    websites: res?.websites ?? res?.Websites ?? 0,
    apps: res?.apps ?? res?.Apps ?? 0,
    subscribers: res?.subscribers ?? res?.Subscribers ?? 0,
    currency: res?.currency ?? res?.Currency ?? 'EUR',
    subscriptions: rows.map(mapRow).filter((r) => r.id.length > 0),
  };
}
