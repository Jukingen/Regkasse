import { customInstance } from '@/lib/axios';

export type CashRegisterOpenRequestStatus = 'Pending' | 'Approved' | 'Denied' | 'Cancelled';

export type CashRegisterOpenRequest = {
  id: string;
  tenantId: string;
  tenantName: string | null;
  tenantSlug: string | null;
  cashRegisterId: string;
  registerNumber: string;
  location: string | null;
  status: CashRegisterOpenRequestStatus;
  requestedByUserId: string;
  requestedByUserName: string | null;
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
  cashRegisterId?: string;
  CashRegisterId?: string;
  registerNumber?: string;
  RegisterNumber?: string;
  location?: string | null;
  Location?: string | null;
  status?: string;
  Status?: string;
  requestedByUserId?: string;
  RequestedByUserId?: string;
  requestedByUserName?: string | null;
  RequestedByUserName?: string | null;
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

function mapRequest(dto: RequestApi): CashRegisterOpenRequest | null {
  const id = dto.id ?? dto.Id ?? '';
  const tenantId = dto.tenantId ?? dto.TenantId ?? '';
  const cashRegisterId = dto.cashRegisterId ?? dto.CashRegisterId ?? '';
  if (!id || !tenantId || !cashRegisterId) return null;
  const statusRaw = (dto.status ?? dto.Status ?? 'Pending') as CashRegisterOpenRequestStatus;
  return {
    id,
    tenantId,
    tenantName: dto.tenantName ?? dto.TenantName ?? null,
    tenantSlug: dto.tenantSlug ?? dto.TenantSlug ?? null,
    cashRegisterId,
    registerNumber: dto.registerNumber ?? dto.RegisterNumber ?? '',
    location: dto.location ?? dto.Location ?? null,
    status: statusRaw,
    requestedByUserId: dto.requestedByUserId ?? dto.RequestedByUserId ?? '',
    requestedByUserName: dto.requestedByUserName ?? dto.RequestedByUserName ?? null,
    requestedAt: dto.requestedAt ?? dto.RequestedAt ?? new Date().toISOString(),
    note: dto.note ?? dto.Note ?? null,
    resolvedByUserId: dto.resolvedByUserId ?? dto.ResolvedByUserId ?? null,
    resolvedAt: dto.resolvedAt ?? dto.ResolvedAt ?? null,
    resolutionNote: dto.resolutionNote ?? dto.ResolutionNote ?? null,
  };
}

function unwrapMutation(res: MutationApi, fallback: string): CashRegisterOpenRequest {
  const ok = res?.succeeded ?? res?.Succeeded ?? false;
  const request = mapRequest(res?.request ?? res?.Request ?? {});
  if (!ok || !request) {
    throw new Error(res?.error ?? res?.Error ?? fallback);
  }
  return request;
}

export async function fetchCashRegisterOpenRequests(
  status: string | null | undefined = 'Pending',
  tenantId?: string
): Promise<CashRegisterOpenRequest[]> {
  const res = await customInstance<RequestApi[]>({
    url: '/api/admin/cash-registers/open-requests',
    method: 'GET',
    params: {
      status: status || 'Pending',
      ...(tenantId ? { tenantId } : {}),
    },
  });
  const rows = Array.isArray(res) ? res : [];
  return rows.map(mapRequest).filter((r): r is CashRegisterOpenRequest => r !== null);
}

export async function approveCashRegisterOpenRequest(
  id: string,
  note?: string
): Promise<CashRegisterOpenRequest> {
  const res = await customInstance<MutationApi>({
    url: `/api/admin/cash-registers/open-requests/${id}/approve`,
    method: 'POST',
    data: { note },
  });
  return unwrapMutation(res, 'Approve failed');
}

export async function denyCashRegisterOpenRequest(
  id: string,
  note?: string
): Promise<CashRegisterOpenRequest> {
  const res = await customInstance<MutationApi>({
    url: `/api/admin/cash-registers/open-requests/${id}/deny`,
    method: 'POST',
    data: { note },
  });
  return unwrapMutation(res, 'Deny failed');
}
