import { customInstance } from '@/lib/axios';

export type CustomerDigitalService = {
  id: string;
  serviceId: string;
  name: string;
  type: string;
  tier: string;
  price: number;
  currency: string;
  status: string;
  createdAt: string;
  nextBillingDate: string;
  url?: string | null;
};

export type MenuSyncResult = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  websiteUrl?: string | null;
  appUrl?: string | null;
};

type CustomerDigitalServiceApi = {
  id?: string;
  Id?: string;
  serviceId?: string;
  ServiceId?: string;
  name?: string;
  Name?: string;
  type?: string;
  Type?: string;
  tier?: string;
  Tier?: string;
  price?: number;
  Price?: number;
  currency?: string;
  Currency?: string;
  status?: string;
  Status?: string;
  createdAt?: string;
  CreatedAt?: string;
  nextBillingDate?: string;
  NextBillingDate?: string;
  url?: string | null;
  Url?: string | null;
};

type MenuSyncApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string | null;
  Code?: string | null;
  error?: string | null;
  Error?: string | null;
  websiteUrl?: string | null;
  WebsiteUrl?: string | null;
  appUrl?: string | null;
  AppUrl?: string | null;
};

function mapService(dto: CustomerDigitalServiceApi): CustomerDigitalService {
  return {
    id: dto.id ?? dto.Id ?? '',
    serviceId: dto.serviceId ?? dto.ServiceId ?? '',
    name: dto.name ?? dto.Name ?? '',
    type: dto.type ?? dto.Type ?? '',
    tier: dto.tier ?? dto.Tier ?? '',
    price: dto.price ?? dto.Price ?? 0,
    currency: dto.currency ?? dto.Currency ?? 'EUR',
    status: dto.status ?? dto.Status ?? '',
    createdAt: dto.createdAt ?? dto.CreatedAt ?? '',
    nextBillingDate: dto.nextBillingDate ?? dto.NextBillingDate ?? '',
    url: dto.url ?? dto.Url,
  };
}

export async function fetchCustomerServices(tenantId?: string): Promise<CustomerDigitalService[]> {
  const res = await customInstance<CustomerDigitalServiceApi[]>({
    url: '/api/admin/website/my-services',
    method: 'GET',
    params: tenantId ? { tenantId } : undefined,
  });
  return (res ?? []).map(mapService).filter((s) => s.id.length > 0);
}

export async function syncDigitalMenu(tenantId?: string): Promise<MenuSyncResult> {
  const res = await customInstance<MenuSyncApi>({
    url: '/api/admin/website/menu-sync',
    method: 'POST',
    params: tenantId ? { tenantId } : undefined,
  });
  return {
    succeeded: res?.succeeded ?? res?.Succeeded ?? false,
    code: res?.code ?? res?.Code,
    error: res?.error ?? res?.Error,
    websiteUrl: res?.websiteUrl ?? res?.WebsiteUrl,
    appUrl: res?.appUrl ?? res?.AppUrl,
  };
}
