/**
 * Admin session management API (`/api/admin/sessions`).
 * Super Admin: all tenants. Manager: ambient tenant only (including POS).
 */
import { AXIOS_INSTANCE } from '@/lib/axios';

export type AdminSessionStatusFilter = 'active' | 'expired' | 'all';

export type AdminSessionListParams = {
  search?: string;
  tenantId?: string;
  role?: string;
  status?: AdminSessionStatusFilter;
  userId?: string;
};

export type AdminActiveSession = {
  id: string;
  userId: string;
  userName?: string | null;
  email?: string | null;
  displayName?: string | null;
  role?: string | null;
  clientApp: string;
  deviceId?: string | null;
  deviceName?: string | null;
  browser?: string | null;
  os?: string | null;
  platformLabel?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  startedAtUtc: string;
  lastActivityAtUtc: string;
  expiresAtUtc?: string | null;
  durationSeconds?: number;
  isActive: boolean;
  isCurrent: boolean;
};

export type TerminateSessionResult = { success: boolean };
export type TerminateSessionsCount = { terminatedCount: number };
export type ForceLogoutResult = { success: boolean };

function compactParams(params: AdminSessionListParams): Record<string, string> {
  const out: Record<string, string> = {};
  if (params.search?.trim()) out.search = params.search.trim();
  if (params.tenantId?.trim()) out.tenantId = params.tenantId.trim();
  if (params.role?.trim()) out.role = params.role.trim();
  if (params.status?.trim()) out.status = params.status.trim();
  if (params.userId?.trim()) out.userId = params.userId.trim();
  return out;
}

export async function fetchAdminSessions(
  params: AdminSessionListParams = {},
  signal?: AbortSignal
): Promise<AdminActiveSession[]> {
  const { data } = await AXIOS_INSTANCE.get<AdminActiveSession[]>('/api/admin/sessions', {
    params: compactParams(params),
    signal,
  });
  return data;
}

export async function fetchAdminUserSessions(
  userId: string,
  signal?: AbortSignal
): Promise<AdminActiveSession[]> {
  const { data } = await AXIOS_INSTANCE.get<AdminActiveSession[]>(
    `/api/admin/sessions/user/${encodeURIComponent(userId)}`,
    { signal }
  );
  return data;
}

export async function terminateAdminSession(sessionId: string): Promise<TerminateSessionResult> {
  const { data } = await AXIOS_INSTANCE.post<TerminateSessionResult>(
    `/api/admin/sessions/${encodeURIComponent(sessionId)}/logout`
  );
  return data;
}

export async function terminateAllUserSessions(userId: string): Promise<TerminateSessionsCount> {
  const { data } = await AXIOS_INSTANCE.post<TerminateSessionsCount>(
    `/api/admin/sessions/user/${encodeURIComponent(userId)}/terminate-all`
  );
  return data;
}

export async function forceLogoutUser(userId: string): Promise<ForceLogoutResult> {
  const { data } = await AXIOS_INSTANCE.post<ForceLogoutResult>(
    `/api/admin/sessions/user/${encodeURIComponent(userId)}/force-logout`
  );
  return data;
}

export async function terminateAllAdminSessions(): Promise<TerminateSessionsCount> {
  const { data } = await AXIOS_INSTANCE.post<TerminateSessionsCount>(
    '/api/admin/sessions/logout-all'
  );
  return data;
}

export async function logoutBulkAdminSessions(
  sessionIds: string[]
): Promise<TerminateSessionsCount> {
  const { data } = await AXIOS_INSTANCE.post<TerminateSessionsCount>(
    '/api/admin/sessions/logout-bulk',
    { sessionIds }
  );
  return data;
}
