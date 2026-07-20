import { customInstance } from '@/lib/axios';

export type DigitalServiceType = 'website' | 'app';

export type DigitalProvisionStatus =
  | 'none'
  | 'pending'
  | 'created'
  | 'published'
  | 'rejected';

export type TenantDigitalServiceState = {
  serviceType: DigitalServiceType;
  isEnabled: boolean;
  isActive: boolean;
  isAvailable: boolean;
  status: DigitalProvisionStatus;
  hasRequest: boolean;
  url: string | null;
  templateId: string | null;
  customization: string | null;
  requestedAt: string | null;
  artifactCreatedAt: string | null;
  publishedAt: string | null;
  price: number;
  customPrice: number | null;
  listPrice: number;
  currency: string;
  activatedAt: string | null;
  deactivatedAt: string | null;
  deactivationReason: string | null;
};

export type TenantDigitalServiceRow = {
  tenantId: string;
  name: string;
  slug: string;
  website: TenantDigitalServiceState;
  app: TenantDigitalServiceState;
};

type StateApi = {
  serviceType?: string;
  ServiceType?: string;
  isEnabled?: boolean;
  IsEnabled?: boolean;
  isActive?: boolean;
  IsActive?: boolean;
  isAvailable?: boolean;
  IsAvailable?: boolean;
  status?: string;
  Status?: string;
  hasRequest?: boolean;
  HasRequest?: boolean;
  url?: string | null;
  Url?: string | null;
  templateId?: string | null;
  TemplateId?: string | null;
  customization?: string | null;
  Customization?: string | null;
  requestedAt?: string | null;
  RequestedAt?: string | null;
  artifactCreatedAt?: string | null;
  ArtifactCreatedAt?: string | null;
  publishedAt?: string | null;
  PublishedAt?: string | null;
  price?: number;
  Price?: number;
  customPrice?: number | null;
  CustomPrice?: number | null;
  listPrice?: number;
  ListPrice?: number;
  currency?: string;
  Currency?: string;
  activatedAt?: string | null;
  ActivatedAt?: string | null;
  deactivatedAt?: string | null;
  DeactivatedAt?: string | null;
  deactivationReason?: string | null;
  DeactivationReason?: string | null;
};

type RowApi = {
  tenantId?: string;
  TenantId?: string;
  name?: string;
  Name?: string;
  slug?: string;
  Slug?: string;
  website?: StateApi;
  Website?: StateApi;
  app?: StateApi;
  App?: StateApi;
};

type MutationApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string;
  Code?: string;
  error?: string;
  Error?: string;
  tenant?: RowApi;
  Tenant?: RowApi;
};

function mapProvisionStatus(raw: string | undefined): DigitalProvisionStatus {
  const value = (raw ?? 'none').toLowerCase();
  if (
    value === 'pending' ||
    value === 'created' ||
    value === 'published' ||
    value === 'rejected'
  ) {
    return value;
  }
  return 'none';
}

function mapState(dto: StateApi | undefined, fallback: DigitalServiceType): TenantDigitalServiceState {
  const typeRaw = (dto?.serviceType ?? dto?.ServiceType ?? fallback).toLowerCase();
  const serviceType: DigitalServiceType = typeRaw === 'app' ? 'app' : 'website';
  const status = mapProvisionStatus(dto?.status ?? dto?.Status);
  return {
    serviceType,
    isEnabled: dto?.isEnabled ?? dto?.IsEnabled ?? true,
    isActive: dto?.isActive ?? dto?.IsActive ?? true,
    isAvailable: dto?.isAvailable ?? dto?.IsAvailable ?? true,
    status,
    hasRequest: dto?.hasRequest ?? dto?.HasRequest ?? status === 'pending',
    url: dto?.url ?? dto?.Url ?? null,
    templateId: dto?.templateId ?? dto?.TemplateId ?? null,
    customization: dto?.customization ?? dto?.Customization ?? null,
    requestedAt: dto?.requestedAt ?? dto?.RequestedAt ?? null,
    artifactCreatedAt: dto?.artifactCreatedAt ?? dto?.ArtifactCreatedAt ?? null,
    publishedAt: dto?.publishedAt ?? dto?.PublishedAt ?? null,
    price: dto?.price ?? dto?.Price ?? 0,
    customPrice: dto?.customPrice ?? dto?.CustomPrice ?? null,
    listPrice: dto?.listPrice ?? dto?.ListPrice ?? 0,
    currency: dto?.currency ?? dto?.Currency ?? 'EUR',
    activatedAt: dto?.activatedAt ?? dto?.ActivatedAt ?? null,
    deactivatedAt: dto?.deactivatedAt ?? dto?.DeactivatedAt ?? null,
    deactivationReason: dto?.deactivationReason ?? dto?.DeactivationReason ?? null,
  };
}

function mapRow(dto: RowApi): TenantDigitalServiceRow | null {
  const tenantId = dto.tenantId ?? dto.TenantId ?? '';
  if (!tenantId) return null;
  return {
    tenantId,
    name: dto.name ?? dto.Name ?? '',
    slug: dto.slug ?? dto.Slug ?? '',
    website: mapState(dto.website ?? dto.Website, 'website'),
    app: mapState(dto.app ?? dto.App, 'app'),
  };
}

export async function fetchTenantDigitalServices(): Promise<TenantDigitalServiceRow[]> {
  const res = await customInstance<RowApi[]>({
    url: '/api/admin/digital/tenants',
    method: 'GET',
  });
  const rows = Array.isArray(res) ? res : [];
  return rows.map(mapRow).filter((r): r is TenantDigitalServiceRow => r !== null);
}

export async function fetchTenantDigitalService(
  tenantId: string,
): Promise<TenantDigitalServiceRow> {
  const res = await customInstance<RowApi>({
    url: `/api/admin/digital/tenants/${tenantId}`,
    method: 'GET',
  });
  const row = mapRow(res ?? {});
  if (!row) {
    throw new Error('Tenant digital service status not found');
  }
  return row;
}

export async function toggleTenantDigitalService(
  tenantId: string,
  serviceType: DigitalServiceType,
  active: boolean,
  reason?: string,
): Promise<TenantDigitalServiceRow> {
  const res = await customInstance<MutationApi>({
    url: `/api/admin/digital/${tenantId}/toggle`,
    method: 'POST',
    data: { serviceType, active, reason },
  });
  const ok = res?.succeeded ?? res?.Succeeded ?? false;
  const tenant = mapRow(res?.tenant ?? res?.Tenant ?? {});
  if (!ok || !tenant) {
    throw new Error(res?.error ?? res?.Error ?? 'Toggle failed');
  }
  return tenant;
}

export async function updateTenantDigitalServicePrice(
  tenantId: string,
  serviceType: DigitalServiceType,
  customPrice: number | null,
): Promise<TenantDigitalServiceRow> {
  const res = await customInstance<MutationApi>({
    url: `/api/admin/digital/${tenantId}/price`,
    method: 'PUT',
    data: { serviceType, customPrice },
  });
  const ok = res?.succeeded ?? res?.Succeeded ?? false;
  const tenant = mapRow(res?.tenant ?? res?.Tenant ?? {});
  if (!ok || !tenant) {
    throw new Error(res?.error ?? res?.Error ?? 'Price update failed');
  }
  return tenant;
}
