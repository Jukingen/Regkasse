/**
 * GET /api/admin/backup/dashboard/health — aggregated widget metrics.
 */
import { customInstance } from '@/lib/axios';

export const BACKUP_DASHBOARD_HEALTH_PATH = '/api/admin/backup/dashboard/health' as const;

export function getBackupDashboardHealthQueryKey() {
  return [BACKUP_DASHBOARD_HEALTH_PATH] as const;
}

export interface BackupDashboardHealthResponseDto {
  healthScore: number;
  healthLevel: string;
  verificationStatus: string;
  lastVerificationRunId?: string | null;
  contentValidationStatus: string;
  contentValidationSummary?: string | null;
  rpoStatus: string;
  rpoHours?: number | null;
  lastSuccessfulBackupAtUtc?: string | null;
}

export async function getBackupDashboardHealth(): Promise<BackupDashboardHealthResponseDto> {
  return customInstance<BackupDashboardHealthResponseDto>({
    url: BACKUP_DASHBOARD_HEALTH_PATH,
    method: 'GET',
  });
}
