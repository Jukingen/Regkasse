import { apiClient } from './config';

export type MaintenanceNotificationDto = {
  id: string;
  title: string;
  message: string;
  scheduledStartAt: string;
  scheduledEndAt: string;
  status: string;
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

export type AcknowledgeMaintenanceNotificationRequest = {
  dismiss?: boolean;
  markRead?: boolean;
};

/** GET /api/pos/maintenance-notifications/active */
export async function fetchActiveMaintenanceNotifications(): Promise<MaintenanceNotificationDto[]> {
  return apiClient.get<MaintenanceNotificationDto[]>('/pos/maintenance-notifications/active');
}

/** POST /api/pos/maintenance-notifications/{id}/acknowledge */
export async function acknowledgeMaintenanceNotification(
  id: string,
  body: AcknowledgeMaintenanceNotificationRequest = { dismiss: true, markRead: true }
): Promise<MaintenanceNotificationDto> {
  return apiClient.post<MaintenanceNotificationDto>(
    `/pos/maintenance-notifications/${id}/acknowledge`,
    body
  );
}
