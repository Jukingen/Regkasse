import { authStorage } from '@/features/auth/services/authStorage';
import { resolveTenantSlugForApiRequest } from '@/features/auth/services/devTenant';
import { TENANT_HTTP_HEADER } from '@/features/auth/services/tenantStorage';
import { AXIOS_INSTANCE } from '@/lib/axios';

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
  signal?: AbortSignal
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

export async function fetchActivityUnreadCount(
  signal?: AbortSignal
): Promise<ActivitiesUnreadCount> {
  const { data } = await AXIOS_INSTANCE.get<ActivitiesUnreadCount>(
    '/api/admin/activities/unread-count',
    {
      signal,
    }
  );
  return data;
}

export async function markActivityRead(id: string): Promise<ActivityDto> {
  const { data } = await AXIOS_INSTANCE.post<ActivityDto>(`/api/admin/activities/${id}/read`);
  return data;
}

export async function markAllActivitiesRead(): Promise<{ markedCount: number }> {
  const { data } = await AXIOS_INSTANCE.post<{ markedCount: number }>(
    '/api/admin/activities/mark-all-read'
  );
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
  depExportMobilePush?: {
    pushEnabled: boolean;
    thirtyDayReminder: boolean;
    sevenDayReminder: boolean;
    oneDayReminder: boolean;
    overdueAlert: boolean;
    successNotification: boolean;
  } | null;
};

export async function fetchNotificationConfig(signal?: AbortSignal): Promise<NotificationConfig> {
  const { data } = await AXIOS_INSTANCE.get<NotificationConfig>(
    '/api/admin/activities/notification-config',
    {
      signal,
    }
  );
  return data;
}

export async function saveNotificationConfig(
  config: NotificationConfig
): Promise<NotificationConfig> {
  const { data } = await AXIOS_INSTANCE.put<NotificationConfig>(
    '/api/admin/activities/notification-config',
    config
  );
  return data;
}

export type ActivityStreamHandlers = {
  onActivity: (activity: ActivityDto) => void;
  onPing?: () => void;
};

export type ActivityStreamSubscribeOptions = {
  signal?: AbortSignal;
  /** Unexpected disconnect retries before giving up. Default 8. */
  maxRetries?: number;
  /** Initial reconnect delay in ms. Default 1000. */
  initialBackoffMs?: number;
  /** Cap for exponential backoff in ms. Default 30000. */
  maxBackoffMs?: number;
};

function isAbortError(error: unknown): boolean {
  if (!error || typeof error !== 'object') {
    return false;
  }
  const name = 'name' in error ? String(error.name) : '';
  return name === 'AbortError';
}

function delay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(signal.reason instanceof Error ? signal.reason : new DOMException('Aborted', 'AbortError'));
      return;
    }

    const timer = setTimeout(() => {
      signal?.removeEventListener('abort', onAbort);
      resolve();
    }, ms);

    const onAbort = () => {
      clearTimeout(timer);
      signal?.removeEventListener('abort', onAbort);
      reject(signal?.reason instanceof Error ? signal.reason : new DOMException('Aborted', 'AbortError'));
    };

    signal?.addEventListener('abort', onAbort, { once: true });
  });
}

/**
 * Keeps an authenticated activity SSE subscription alive with bounded reconnect.
 * Stops immediately when `signal` is aborted (component unmount / disabled).
 */
export async function subscribeActivityStream(
  handlers: ActivityStreamHandlers,
  options?: ActivityStreamSubscribeOptions
): Promise<void> {
  const signal = options?.signal;
  const maxRetries = options?.maxRetries ?? 8;
  const initialBackoffMs = options?.initialBackoffMs ?? 1_000;
  const maxBackoffMs = options?.maxBackoffMs ?? 30_000;

  let attempt = 0;

  while (!signal?.aborted) {
    try {
      await connectActivityStream(handlers, signal);
      if (signal?.aborted) {
        return;
      }

      // Server closed the stream cleanly — reconnect with backoff.
      attempt += 1;
      if (attempt > maxRetries) {
        return;
      }
    } catch (error) {
      if (signal?.aborted || isAbortError(error)) {
        return;
      }

      attempt += 1;
      if (attempt > maxRetries) {
        throw error;
      }
    }

    const backoffMs = Math.min(maxBackoffMs, initialBackoffMs * 2 ** (attempt - 1));
    try {
      await delay(backoffMs, signal);
    } catch (error) {
      if (signal?.aborted || isAbortError(error)) {
        return;
      }
      throw error;
    }
  }
}

/** Authenticated SSE via fetch (supports Authorization header). Single connection attempt. */
export async function connectActivityStream(
  handlers: ActivityStreamHandlers,
  signal?: AbortSignal
): Promise<void> {
  if (!API_BASE) {
    throw new Error('NEXT_PUBLIC_API_BASE_URL is not configured.');
  }

  if (signal?.aborted) {
    return;
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

  let response: Response;
  try {
    response = await fetch(`${API_BASE}/api/admin/activities/stream`, {
      method: 'GET',
      headers,
      signal,
    });
  } catch (error) {
    if (signal?.aborted || isAbortError(error)) {
      return;
    }
    throw error;
  }

  if (!response.ok) {
    throw new Error(`Activity stream failed: ${response.status}`);
  }

  const reader = response.body?.getReader();
  if (!reader) {
    return;
  }

  const decoder = new TextDecoder();
  let buffer = '';

  try {
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
  } catch (error) {
    if (signal?.aborted || isAbortError(error)) {
      return;
    }
    throw error;
  } finally {
    try {
      await reader.cancel();
    } catch {
      // Reader may already be closed after abort/disconnect.
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
