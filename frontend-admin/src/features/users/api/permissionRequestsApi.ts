import { customInstance } from '@/lib/axios';

export type PermissionRequestStatus = 'Pending' | 'Approved' | 'Rejected' | string;

export type PermissionRequestDto = {
  id: string;
  tenantId?: string | null;
  requesterUserId: string;
  requesterUserName?: string | null;
  permission: string;
  reason: string;
  requestedDuration: string;
  requestedExpiresAt?: string | null;
  status: PermissionRequestStatus;
  requestedAt: string;
  resolvedByUserId?: string | null;
  resolvedAt?: string | null;
  resolutionNote?: string | null;
  resultingOverrideId?: string | null;
};

export type CreatePermissionRequestBody = {
  permission: string;
  reason: string;
  duration: '1d' | '7d' | '30d' | string;
  customExpiresAt?: string | null;
};

export type ResolvePermissionRequestBody = {
  note?: string | null;
  expiresAt?: string | null;
};

export type PermissionRequestStatsDto = {
  pending: number;
  approved: number;
  rejected: number;
  total: number;
};

type MutationResult = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string;
  Code?: string;
  error?: string;
  Error?: string;
  request?: Record<string, unknown>;
  Request?: Record<string, unknown>;
};

function pickString(row: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = row[key];
    if (typeof value === 'string' && value.trim()) return value;
  }
  return '';
}

function pickNullableString(row: Record<string, unknown>, ...keys: string[]): string | null {
  for (const key of keys) {
    const value = row[key];
    if (value == null) continue;
    if (typeof value === 'string') return value;
  }
  return null;
}

function mapRequest(raw: Record<string, unknown> | undefined | null): PermissionRequestDto | null {
  if (!raw) return null;
  const id = pickString(raw, 'id', 'Id');
  if (!id) return null;
  return {
    id,
    tenantId: pickNullableString(raw, 'tenantId', 'TenantId'),
    requesterUserId: pickString(raw, 'requesterUserId', 'RequesterUserId'),
    requesterUserName: pickNullableString(raw, 'requesterUserName', 'RequesterUserName'),
    permission: pickString(raw, 'permission', 'Permission'),
    reason: pickString(raw, 'reason', 'Reason'),
    requestedDuration: pickString(raw, 'requestedDuration', 'RequestedDuration'),
    requestedExpiresAt: pickNullableString(raw, 'requestedExpiresAt', 'RequestedExpiresAt'),
    status: pickString(raw, 'status', 'Status') || 'Pending',
    requestedAt: pickString(raw, 'requestedAt', 'RequestedAt') || new Date().toISOString(),
    resolvedByUserId: pickNullableString(raw, 'resolvedByUserId', 'ResolvedByUserId'),
    resolvedAt: pickNullableString(raw, 'resolvedAt', 'ResolvedAt'),
    resolutionNote: pickNullableString(raw, 'resolutionNote', 'ResolutionNote'),
    resultingOverrideId: pickNullableString(raw, 'resultingOverrideId', 'ResultingOverrideId'),
  };
}

function unwrapMutation(res: MutationResult): PermissionRequestDto {
  const ok = res?.succeeded ?? res?.Succeeded ?? false;
  const request = mapRequest(res?.request ?? res?.Request ?? null);
  if (!ok || !request) {
    throw new Error(res?.error ?? res?.Error ?? 'Permission request mutation failed');
  }
  return request;
}

export async function createPermissionRequest(
  body: CreatePermissionRequestBody
): Promise<PermissionRequestDto> {
  const res = await customInstance<MutationResult>({
    url: '/api/admin/permission-requests',
    method: 'POST',
    data: body,
  });
  return unwrapMutation(res);
}

export async function fetchMyPermissionRequests(): Promise<PermissionRequestDto[]> {
  const res = await customInstance<Record<string, unknown>[]>({
    url: '/api/admin/permission-requests/mine',
    method: 'GET',
  });
  const rows = Array.isArray(res) ? res : [];
  return rows.map(mapRequest).filter((r): r is PermissionRequestDto => r !== null);
}

export async function fetchPendingPermissionRequests(): Promise<PermissionRequestDto[]> {
  const res = await customInstance<Record<string, unknown>[]>({
    url: '/api/admin/permission-requests/pending',
    method: 'GET',
  });
  const rows = Array.isArray(res) ? res : [];
  return rows.map(mapRequest).filter((r): r is PermissionRequestDto => r !== null);
}

export async function fetchPermissionRequestStats(): Promise<PermissionRequestStatsDto> {
  const res = await customInstance<Record<string, unknown>>({
    url: '/api/admin/permission-requests/stats',
    method: 'GET',
  });
  return {
    pending: Number(res?.pending ?? res?.Pending ?? 0),
    approved: Number(res?.approved ?? res?.Approved ?? 0),
    rejected: Number(res?.rejected ?? res?.Rejected ?? 0),
    total: Number(res?.total ?? res?.Total ?? 0),
  };
}

export async function approvePermissionRequest(
  id: string,
  body?: ResolvePermissionRequestBody
): Promise<PermissionRequestDto> {
  const res = await customInstance<MutationResult>({
    url: `/api/admin/permission-requests/${id}/approve`,
    method: 'POST',
    data: body ?? {},
  });
  return unwrapMutation(res);
}

export async function rejectPermissionRequest(
  id: string,
  body?: ResolvePermissionRequestBody
): Promise<PermissionRequestDto> {
  const res = await customInstance<MutationResult>({
    url: `/api/admin/permission-requests/${id}/reject`,
    method: 'POST',
    data: body ?? {},
  });
  return unwrapMutation(res);
}
