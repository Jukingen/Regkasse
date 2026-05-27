import { AXIOS_INSTANCE } from '@/lib/axios';
import { authStorage } from '@/features/auth/services/authStorage';
import { resolveTenantSlugForApiRequest } from '@/features/auth/services/devTenant';
import { TENANT_HTTP_HEADER } from '@/features/auth/services/tenantStorage';

const API_BASE =
    process.env.NEXT_PUBLIC_API_BASE_URL ||
    (process.env.NODE_ENV === 'development' ? 'http://localhost:5184' : '');

export type ActivitySeverity = 'Info' | 'Warning' | 'Error' | 'Critical';

export type ActivityDto = {
    id: string;
    type: string;
    severity: ActivitySeverity;
    title: string;
    description?: string | null;
    actorUserId?: string | null;
    actorName?: string | null;
    entityId?: string | null;
    entityType?: string | null;
    metadata?: Record<string, unknown> | null;
    isRead: boolean;
    createdAtUtc: string;
    readAtUtc?: string | null;
};

export type ActivitiesListResponse = {
    items: ActivityDto[];
    total: number;
    limit: number;
    offset: number;
};

export type ActivitiesUnreadCount = {
    unreadCount: number;
};

export async function fetchActivities(
    params: { limit?: number; offset?: number; severity?: ActivitySeverity },
    signal?: AbortSignal,
): Promise<ActivitiesListResponse> {
    const { data } = await AXIOS_INSTANCE.get<ActivitiesListResponse>('/api/admin/activities', {
        params: {
            limit: params.limit ?? 50,
            offset: params.offset ?? 0,
            severity: params.severity,
        },
        signal,
    });
    return data;
}

export async function fetchActivityUnreadCount(signal?: AbortSignal): Promise<ActivitiesUnreadCount> {
    const { data } = await AXIOS_INSTANCE.get<ActivitiesUnreadCount>('/api/admin/activities/unread-count', {
        signal,
    });
    return data;
}

export async function markActivityRead(id: string): Promise<ActivityDto> {
    const { data } = await AXIOS_INSTANCE.post<ActivityDto>(`/api/admin/activities/${id}/read`);
    return data;
}

export async function markAllActivitiesRead(): Promise<{ markedCount: number }> {
    const { data } = await AXIOS_INSTANCE.post<{ markedCount: number }>('/api/admin/activities/mark-all-read');
    return data;
}

export type NotificationConfig = {
    inAppEnabled: boolean;
    emailEnabled: boolean;
    emailRecipients: string[];
    webhookEnabled: boolean;
    webhookUrl?: string | null;
    webhookSecret?: string | null;
    enabledEvents: Record<string, boolean>;
    severityThreshold: Record<string, string>;
};

export async function fetchNotificationConfig(signal?: AbortSignal): Promise<NotificationConfig> {
    const { data } = await AXIOS_INSTANCE.get<NotificationConfig>('/api/admin/activities/notification-config', {
        signal,
    });
    return data;
}

export async function saveNotificationConfig(config: NotificationConfig): Promise<NotificationConfig> {
    const { data } = await AXIOS_INSTANCE.put<NotificationConfig>(
        '/api/admin/activities/notification-config',
        config,
    );
    return data;
}

export type ActivityStreamHandlers = {
    onActivity: (activity: ActivityDto) => void;
    onPing?: () => void;
};

/** Authenticated SSE via fetch (supports Authorization header). */
export async function connectActivityStream(
    handlers: ActivityStreamHandlers,
    signal?: AbortSignal,
): Promise<void> {
    if (!API_BASE) {
        throw new Error('NEXT_PUBLIC_API_BASE_URL is not configured.');
    }

    const headers: Record<string, string> = {
        Accept: 'text/event-stream',
    };
    const token = authStorage.getToken();
    if (token) {
        headers.Authorization = `Bearer ${token}`;
    }
    const tenantSlug = resolveTenantSlugForApiRequest();
    if (tenantSlug) {
        headers[TENANT_HTTP_HEADER] = tenantSlug;
    }

    const response = await fetch(`${API_BASE}/api/admin/activities/stream`, {
        method: 'GET',
        headers,
        signal,
    });

    if (!response.ok) {
        throw new Error(`Activity stream failed: ${response.status}`);
    }

    const reader = response.body?.getReader();
    if (!reader) {
        return;
    }

    const decoder = new TextDecoder();
    let buffer = '';

    while (!signal?.aborted) {
        const { done, value } = await reader.read();
        if (done) {
            break;
        }

        buffer += decoder.decode(value, { stream: true });
        const frames = buffer.split('\n\n');
        buffer = frames.pop() ?? '';

        for (const frame of frames) {
            if (!frame.trim()) {
                continue;
            }
            const parsed = parseSseFrame(frame);
            if (!parsed) {
                continue;
            }
            if (parsed.event === 'ping') {
                handlers.onPing?.();
                continue;
            }
            if (parsed.event === 'activity' && parsed.data) {
                try {
                    const activity = JSON.parse(parsed.data) as ActivityDto;
                    handlers.onActivity(activity);
                } catch {
                    // ignore malformed frames
                }
            }
        }
    }
}

function parseSseFrame(frame: string): { event: string; data: string } | null {
    let eventName = 'message';
    const dataLines: string[] = [];
    for (const line of frame.split('\n')) {
        if (line.startsWith('event:')) {
            eventName = line.slice(6).trim();
        } else if (line.startsWith('data:')) {
            dataLines.push(line.slice(5).trimStart());
        }
    }
    return { event: eventName, data: dataLines.join('\n') };
}
