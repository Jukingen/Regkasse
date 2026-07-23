/**
 * Platform maintenance mode API (status / start / end) + notification list helpers.
 */
import { AXIOS_INSTANCE } from '@/lib/axios';

import {
  fetchActiveMaintenanceNotifications,
  fetchAllMaintenanceNotifications,
  type MaintenanceNotificationDto,
  type MaintenanceNotificationListResponse,
} from './maintenanceNotifications';

export type MaintenanceModeStatusDto = {
  isActive: boolean;
  notificationId?: string | null;
  title?: string | null;
  message?: string | null;
  startedAt?: string | null;
  scheduledStartAt?: string | null;
  scheduledEndAt?: string | null;
  status: string;
  blocksPosPayments: boolean;
  blocksApiWrites: boolean;
};

export type StartMaintenanceModeRequest = {
  scheduledEndAt?: string;
  title?: string;
  message?: string;
  priority?: number;
  isMandatory?: boolean;
};

export async function getMaintenanceStatus(
  signal?: AbortSignal,
): Promise<MaintenanceModeStatusDto> {
  const { data } = await AXIOS_INSTANCE.get<MaintenanceModeStatusDto>(
    '/api/admin/maintenance/status',
    { signal },
  );
  return data;
}

export async function startMaintenance(
  body: StartMaintenanceModeRequest = {},
): Promise<MaintenanceModeStatusDto> {
  const { data } = await AXIOS_INSTANCE.post<MaintenanceModeStatusDto>(
    '/api/admin/maintenance/start',
    body,
  );
  return data;
}

export async function endMaintenance(): Promise<MaintenanceModeStatusDto> {
  const { data } = await AXIOS_INSTANCE.post<MaintenanceModeStatusDto>(
    '/api/admin/maintenance/end',
  );
  return data;
}

export {
  fetchActiveMaintenanceNotifications,
  fetchAllMaintenanceNotifications,
  type MaintenanceNotificationDto,
  type MaintenanceNotificationListResponse,
};
