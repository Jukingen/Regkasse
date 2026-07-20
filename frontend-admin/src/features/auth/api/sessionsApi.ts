import { customInstance } from '@/lib/axios';

export type ActiveSession = {
    id: string;
    userId: string;
    clientApp: string;
    deviceId?: string | null;
    deviceName?: string | null;
    browser?: string | null;
    os?: string | null;
    ipAddress?: string | null;
    userAgent?: string | null;
    startedAtUtc: string;
    lastActivityAtUtc: string;
    expiresAtUtc?: string | null;
    isActive: boolean;
    isCurrent: boolean;
};

export async function fetchMySessions(): Promise<ActiveSession[]> {
    const res = await customInstance<ActiveSession[]>({
        url: '/api/user/sessions',
        method: 'GET',
    });
    return res;
}

export async function terminateSession(sessionId: string): Promise<void> {
    await customInstance({
        url: `/api/user/sessions/${sessionId}`,
        method: 'DELETE',
    });
}

export async function terminateAllOtherSessions(): Promise<number> {
    const res = await customInstance<{ terminatedCount?: number }>({
        url: '/api/user/sessions/terminate-all',
        method: 'POST',
    });
    return res.terminatedCount ?? 0;
}

export async function sendSessionHeartbeat(): Promise<void> {
    await customInstance({
        url: '/api/user/sessions/heartbeat',
        method: 'POST',
    });
}
