/**
 * GET /api/admin/backup/dashboard/stats — manual client until Orval regeneration.
 */
import type { BackupArtifactPipelinePolicyResponseDto } from '@/api/generated/model/backupArtifactPipelinePolicyResponseDto';
import type { BackupConfigurationHealthResponseDto } from '@/api/generated/model/backupConfigurationHealthResponseDto';
import { customInstance } from '@/lib/axios';

export const BACKUP_DASHBOARD_STATS_PATH = '/api/admin/backup/dashboard/stats' as const;

export const BACKUP_DASHBOARD_STATS_POLL_MS = 30_000;

export function getBackupDashboardStatsQueryKey() {
  return [BACKUP_DASHBOARD_STATS_PATH] as const;
}

export interface BackupDashboardHistoryPointDto {
  runId: string;
  completedAtUtc: string;
  status: number;
  success: number;
  failed: number;
  durationSeconds: number;
}

export interface BackupDashboardStatsResponseDto {
  lastBackupAtUtc?: string | null;
  lastBackupStatus?: number;
  lastBackupRunId?: string | null;
  lastSuccessfulBackupAtUtc?: string | null;
  backupSizeBytes?: number | null;
  successRate30DaysPercent?: number | null;
  successRateTrendVsPrior30DaysPercent?: number | null;
  terminalRuns30Days?: number;
  succeededRuns30Days?: number;
  failedRuns30Days?: number;
  pendingRunsCount?: number;
  totalRuns30Days?: number;
  nextScheduledBackupAtUtc?: string | null;
  stagingDiskUsedPercent?: number | null;
  stagingDiskAlert?: boolean;
  rpoHours?: number | null;
  rtoMinutes?: number | null;
  lastSuccessfulRestoreDrillAtUtc?: string | null;
  latestRestoreDrillStatus?: number;
  lastVerifiedBackupAtUtc?: string | null;
  averageSucceededBackupDurationSeconds?: number | null;
  averageSucceededBackupDurationSampleCount?: number;
  configurationHealth?: BackupConfigurationHealthResponseDto;
  artifactPipelinePolicy?: BackupArtifactPipelinePolicyResponseDto;
  history30Days?: BackupDashboardHistoryPointDto[] | null;
}

export async function getBackupDashboardStats(): Promise<BackupDashboardStatsResponseDto> {
  return customInstance<BackupDashboardStatsResponseDto>({
    url: BACKUP_DASHBOARD_STATS_PATH,
    method: 'GET',
  });
}
