import { customInstance } from '@/lib/axios';
import type { DigitalServiceType } from '@/features/digital-services/api/tenantDigitalServicesApi';

export type DigitalServiceRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export type DigitalServiceRequest = {
  id: string;
  tenantId: string;
  tenantName: string | null;
  tenantSlug: string | null;
  serviceType: DigitalServiceType;
  status: DigitalServiceRequestStatus;
  requestedByUserId: string | null;
  requestedAt: string;
  note: string | null;
  resolvedByUserId: string | null;
  resolvedAt: string | null;
  resolutionNote: string | null;
};

type RequestApi = {
  id?: string;
  Id?: string;
  tenantId?: string;
  TenantId?: string;
  tenantName?: string | null;
  TenantName?: string | null;
  tenantSlug?: string | null;
  TenantSlug?: string | null;
  serviceType?: string;
  ServiceType?: string;
  status?: string;
  Status?: string;
  requestedByUserId?: string | null;
  RequestedByUserId?: string | null;
  requestedAt?: string;
  RequestedAt?: string;
  note?: string | null;
  Note?: string | null;
  resolvedByUserId?: string | null;
  ResolvedByUserId?: string | null;
  resolvedAt?: string | null;
  ResolvedAt?: string | null;
  resolutionNote?: string | null;
  ResolutionNote?: string | null;
};

type MutationApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string;
  Code?: string;
  error?: string;
  Error?: string;
  request?: RequestApi;
  Request?: RequestApi;
};

function mapRequest(dto: RequestApi): DigitalServiceRequest | null {
  const id = dto.id ?? dto.Id ?? '';
  const tenantId = dto.tenantId ?? dto.TenantId ?? '';
  if (!id || !tenantId) return null;
  const typeRaw = (dto.serviceType ?? dto.ServiceType ?? 'website').toLowerCase();
  const statusRaw = (dto.status ?? dto.Status ?? 'Pending') as DigitalServiceRequestStatus;
  return {
    id,
    tenantId,
    tenantName: dto.tenantName ?? dto.TenantName ?? null,
    tenantSlug: dto.tenantSlug ?? dto.TenantSlug ?? null,
    serviceType: typeRaw === 'app' ? 'app' : 'website',
    status: statusRaw,
    requestedByUserId: dto.requestedByUserId ?? dto.RequestedByUserId ?? null,
    requestedAt: dto.requestedAt ?? dto.RequestedAt ?? new Date().toISOString(),
    note: dto.note ?? dto.Note ?? null,
    resolvedByUserId: dto.resolvedByUserId ?? dto.ResolvedByUserId ?? null,
    resolvedAt: dto.resolvedAt ?? dto.ResolvedAt ?? null,
    resolutionNote: dto.resolutionNote ?? dto.ResolutionNote ?? null,
  };
}

export async function requestDigitalService(
  tenantId: string,
  serviceType: DigitalServiceType,
  note?: string,
): Promise<DigitalServiceRequest> {
  const res = await customInstance<MutationApi>({
    url: `/api/admin/digital/${tenantId}/request`,
    method: 'POST',
    data: { serviceType, note },
  });
  const ok = res?.succeeded ?? res?.Succeeded ?? false;
  const request = mapRequest(res?.request ?? res?.Request ?? {});
  if (!ok || !request) {
    throw new Error(res?.error ?? res?.Error ?? 'Request failed');
  }
  return request;
}

export async function fetchTenantDigitalServiceRequests(
  tenantId: string,
  status?: string,
): Promise<DigitalServiceRequest[]> {
  const res = await customInstance<RequestApi[]>({
    url: `/api/admin/digital/${tenantId}/requests`,
    method: 'GET',
    params: status ? { status } : undefined,
  });
  const rows = Array.isArray(res) ? res : [];
  return rows.map(mapRequest).filter((r): r is DigitalServiceRequest => r !== null);
}

export async function fetchDigitalServiceRequests(
  status: string | null | undefined = 'Pending',
): Promise<DigitalServiceRequest[]> {
  const res = await customInstance<RequestApi[]>({
    url: '/api/admin/digital/requests',
    method: 'GET',
    params: status ? { status } : { status: 'Pending' },
  });
  const rows = Array.isArray(res) ? res : [];
  return rows.map(mapRequest).filter((r): r is DigitalServiceRequest => r !== null);
}

export async function approveDigitalServiceRequest(
  id: string,
  note?: string,
): Promise<DigitalServiceRequest> {
  const res = await customInstance<MutationApi>({
    url: `/api/admin/digital/requests/${id}/approve`,
    method: 'POST',
    data: { note },
  });
  const ok = res?.succeeded ?? res?.Succeeded ?? false;
  const request = mapRequest(res?.request ?? res?.Request ?? {});
  if (!ok || !request) {
    throw new Error(res?.error ?? res?.Error ?? 'Approve failed');
  }
  return request;
}

export async function rejectDigitalServiceRequest(
  id: string,
  note?: string,
): Promise<DigitalServiceRequest> {
  const res = await customInstance<MutationApi>({
    url: `/api/admin/digital/requests/${id}/reject`,
    method: 'POST',
    data: { note },
  });
  const ok = res?.succeeded ?? res?.Succeeded ?? false;
  const request = mapRequest(res?.request ?? res?.Request ?? {});
  if (!ok || !request) {
    throw new Error(res?.error ?? res?.Error ?? 'Reject failed');
  }
  return request;
}
