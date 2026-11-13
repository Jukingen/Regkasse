/**
 * Manual admin API client for platform maintenance notifications.
 */
import { AXIOS_INSTANCE } from '@/lib/axios';

export type MaintenanceNotificationStatus =
  | 'Draft'
  | 'Published'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled';

export type MaintenanceNotificationDto = {
  id: string;
  title: string;
  message: string;
  scheduledStartAt: string;
  scheduledEndAt: string;
  status: MaintenanceNotificationStatus | string;
  priority: number;
  isMandatory: boolean;
  isForceDisplay: boolean;
  forceDisplayFrom?: string | null;
  affectedSystems: string;
  createdBy: string;
  createdAt: string;
  publishedAt?: string | null;
  effectiveForceDisplay: boolean;
  canDismiss: boolean;
  isDismissedByCurrentUser: boolean;
  isReadByCurrentUser: boolean;
};

export type MaintenanceNotificationListResponse = {
  items: MaintenanceNotificationDto[];
  total: number;
};

export type AcknowledgeMaintenanceNotificationRequest = {
  dismiss?: boolean;
  markRead?: boolean;
};

export async function fetchActiveMaintenanceNotifications(
  signal?: AbortSignal,
): Promise<MaintenanceNotificationDto[]> {
  const { data } = await AXIOS_INSTANCE.get<MaintenanceNotificationDto[]>(
    '/api/admin/maintenance-notifications/active',
    { signal },
  );
  return data;
}

export async function acknowledgeMaintenanceNotification(
  id: string,
  body: AcknowledgeMaintenanceNotificationRequest = { dismiss: true, markRead: true },
): Promise<MaintenanceNotificationDto> {
  const { data } = await AXIOS_INSTANCE.post<MaintenanceNotificationDto>(
    `/api/admin/maintenance-notifications/${id}/acknowledge`,
    body,
  );
  return data;
}

export async function fetchAllMaintenanceNotifications(params?: {
  status?: string;
  limit?: number;
  offset?: number;
}): Promise<MaintenanceNotificationListResponse> {
  const { data } = await AXIOS_INSTANCE.get<MaintenanceNotificationListResponse>(
    '/api/admin/maintenance-notifications',
    {
      params: {
        status: params?.status,
        limit: params?.limit ?? 50,
        offset: params?.offset ?? 0,
      },
    },
  );
  return data;
}
