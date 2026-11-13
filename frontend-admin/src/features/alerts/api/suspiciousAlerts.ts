import type { SuspiciousAlertsListResponse } from '@/features/alerts/types';
import { customInstance } from '@/lib/axios';

export async function fetchSuspiciousAlerts(
  params: { unreadOnly?: boolean },
  signal?: AbortSignal
): Promise<SuspiciousAlertsListResponse> {
  return customInstance<SuspiciousAlertsListResponse>({
    url: '/api/admin/payments/alerts',
    method: 'GET',
    params: { unreadOnly: params.unreadOnly ?? true },
    signal,
  });
}

export async function markSuspiciousAlertRead(
  alertId: string,
  signal?: AbortSignal
): Promise<{ success: boolean }> {
  return customInstance<{ success: boolean }>({
    url: `/api/admin/payments/alerts/${alertId}/read`,
    method: 'POST',
    signal,
  });
}
