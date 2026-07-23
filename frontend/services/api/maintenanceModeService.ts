import { apiClient } from './config';

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

/** GET /api/pos/maintenance/status */
export async function checkMaintenanceStatus(): Promise<MaintenanceModeStatusDto> {
  return apiClient.get<MaintenanceModeStatusDto>('/pos/maintenance/status');
}
