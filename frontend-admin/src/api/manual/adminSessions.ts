/**
 * Super Admin session management API (`/api/admin/sessions`).
 */
import { AXIOS_INSTANCE } from '@/lib/axios';

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
  ipAddress?: string | null;
  userAgent?: string | null;
  tenantId?: string | null;
  startedAtUtc: string;
  lastActivityAtUtc: string;
  expiresAtUtc?: string | null;
  isActive: boolean;
  isCurrent: boolean;
};

export type TerminateSessionResult = { success: boolean };
export type TerminateSessionsCount = { terminatedCount: number };
export type ForceLogoutResult = { success: boolean };

export async function fetchAdminSessions(signal?: AbortSignal): Promise<AdminActiveSession[]> {
  const { data } = await AXIOS_INSTANCE.get<AdminActiveSession[]>('/api/admin/sessions', {
    signal,
  });
  return data;
}

export async function fetchAdminUserSessions(
  userId: string,
  signal?: AbortSignal,
): Promise<AdminActiveSession[]> {
  const { data } = await AXIOS_INSTANCE.get<AdminActiveSession[]>(
    `/api/admin/sessions/user/${encodeURIComponent(userId)}`,
    { signal },
  );
  return data;
}

export async function terminateAdminSession(sessionId: string): Promise<TerminateSessionResult> {
  const { data } = await AXIOS_INSTANCE.post<TerminateSessionResult>(
    `/api/admin/sessions/${encodeURIComponent(sessionId)}/terminate`,
  );
  return data;
}

export async function terminateAllUserSessions(userId: string): Promise<TerminateSessionsCount> {
  const { data } = await AXIOS_INSTANCE.post<TerminateSessionsCount>(
    `/api/admin/sessions/user/${encodeURIComponent(userId)}/terminate-all`,
  );
  return data;
}

export async function forceLogoutUser(userId: string): Promise<ForceLogoutResult> {
  const { data } = await AXIOS_INSTANCE.post<ForceLogoutResult>(
    `/api/admin/sessions/user/${encodeURIComponent(userId)}/force-logout`,
  );
  return data;
}

export async function terminateAllAdminSessions(): Promise<TerminateSessionsCount> {
  const { data } = await AXIOS_INSTANCE.post<TerminateSessionsCount>(
    '/api/admin/sessions/terminate-all',
  );
  return data;
}
