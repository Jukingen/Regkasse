/**
 * Restore readiness kartı: RPO/RTO eşikleri ve drill özet alanları (saf türetim).
 */
import type {
  BackupRecoverabilitySummaryResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import { RestoreVerificationStatus } from '@/api/generated/model/restoreVerificationStatus';
import {
  type MetricStatus,
  estimateRpoSeconds,
  estimateRtoSeconds,
} from '@/features/backup-dr/logic/backupMonitoringMetrics';

export type ReadinessThresholdStatus = MetricStatus;

const RPO_ERROR_HOURS = 24;
const RPO_WARN_HOURS = 12;
const RTO_ERROR_MINUTES = 60;
const RTO_WARN_MINUTES = 30;

export function thresholdStatusFromRpoHours(hours: number): ReadinessThresholdStatus {
  if (hours > RPO_ERROR_HOURS) return 'error';
  if (hours > RPO_WARN_HOURS) return 'warning';
  return 'success';
}

export function thresholdStatusFromRtoMinutes(minutes: number): ReadinessThresholdStatus {
  if (minutes > RTO_ERROR_MINUTES) return 'error';
  if (minutes > RTO_WARN_MINUTES) return 'warning';
  return 'success';
}

export function rpoProgressPercent(hours: number): number {
  return Math.min(100, Math.max(0, ((RPO_ERROR_HOURS - hours) / RPO_ERROR_HOURS) * 100));
}

export function rtoProgressPercent(minutes: number): number {
  return Math.min(100, Math.max(0, ((RTO_ERROR_MINUTES - minutes) / RTO_ERROR_MINUTES) * 100));
}

export function metricStatusToStatisticColor(status: ReadinessThresholdStatus): string | undefined {
  if (status === 'error') return '#ff4d4f';
  if (status === 'warning') return '#faad14';
  if (status === 'success') return '#52c41a';
  return undefined;
}

export function metricStatusToProgressStatus(
  status: ReadinessThresholdStatus
): 'success' | 'exception' | 'active' | 'normal' {
  if (status === 'error') return 'exception';
  return 'active';
}

export type RestoreDrillBadgeStatus = 'success' | 'error' | 'processing' | 'default';

export interface RestoreReadinessViewModel {
  rpoHours: number | null;
  rtoMinutes: number | null;
  rpoStatus: ReadinessThresholdStatus;
  rtoStatus: ReadinessThresholdStatus;
  rpoProgressPercent: number;
  rtoProgressPercent: number;
  lastSuccessfulDrillAt: string | null;
  drillBadgeStatus: RestoreDrillBadgeStatus;
  lastVerifiedBackupAt: string | null;
}

export function mapRestoreDrillBadgeStatus(status: number | undefined): RestoreDrillBadgeStatus {
  if (status === RestoreVerificationStatus.NUMBER_2) return 'success';
  if (status === RestoreVerificationStatus.NUMBER_3) return 'error';
  if (
    status === RestoreVerificationStatus.NUMBER_0 ||
    status === RestoreVerificationStatus.NUMBER_1
  ) {
    return 'processing';
  }
  return 'default';
}

export function buildRestoreReadinessViewModel(params: {
  recoverability: BackupRecoverabilitySummaryResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  averageSucceededBackupDurationSeconds?: number | null;
}): RestoreReadinessViewModel {
  const rpoSec = estimateRpoSeconds(params.recoverability?.lastSuccessfulBackupAt);
  const rtoSec = estimateRtoSeconds({
    averageSucceededBackupDurationSeconds: params.averageSucceededBackupDurationSeconds,
    restoreProofAgeSeconds: params.recoverability?.restoreProofAgeSeconds,
  });

  const rpoHours = rpoSec !== undefined ? rpoSec / 3600 : null;
  const rtoMinutes = rtoSec !== undefined ? rtoSec / 60 : null;

  const rpoStatus = rpoHours !== null ? thresholdStatusFromRpoHours(rpoHours) : 'info';
  const rtoStatus = rtoMinutes !== null ? thresholdStatusFromRtoMinutes(rtoMinutes) : 'info';

  return {
    rpoHours,
    rtoMinutes,
    rpoStatus,
    rtoStatus,
    rpoProgressPercent: rpoHours !== null ? rpoProgressPercent(rpoHours) : 0,
    rtoProgressPercent: rtoMinutes !== null ? rtoProgressPercent(rtoMinutes) : 0,
    lastSuccessfulDrillAt: params.recoverability?.lastSuccessfulRestoreProofAt?.trim() || null,
    drillBadgeStatus: mapRestoreDrillBadgeStatus(params.restoreLatest?.status),
    lastVerifiedBackupAt:
      params.recoverability?.lastSuccessfulArtifactVerificationAt?.trim() ||
      params.recoverability?.lastSuccessfulBackupAt?.trim() ||
      null,
  };
}
