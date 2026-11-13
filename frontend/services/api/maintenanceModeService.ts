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
  return await apiClient.get<MaintenanceModeStatusDto>('/pos/maintenance/status');
}

/**
 * End platform maintenance (Super Admin only).
 * Same endpoint as FA — requires system.critical; middleware already bypasses Super Admin writes.
 */
export async function endMaintenance(): Promise<MaintenanceModeStatusDto> {
  return await apiClient.post<MaintenanceModeStatusDto>('/admin/maintenance/end');
}
