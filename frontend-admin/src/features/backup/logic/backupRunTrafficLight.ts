import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import { resolveBackupRunStatusUiKey } from '@/features/backup/logic/backupRunTablePresentation';

export type BackupRunTrafficLight = 'success' | 'pending' | 'failed' | 'neutral';

/** P0 list semantics: green succeeded, yellow pending, red failed. */
export function resolveBackupRunTrafficLight(status: number | undefined): BackupRunTrafficLight {
  const key = resolveBackupRunStatusUiKey(status);
  if (key === 'succeeded') return 'success';
  if (key === 'failed' || key === 'verificationFailed') return 'failed';
  if (key === 'queued' || key === 'running' || key === 'awaitingVerification') return 'pending';
  return 'neutral';
}

export function backupRunTrafficLightTagColor(
  light: BackupRunTrafficLight
): 'success' | 'warning' | 'error' | 'default' {
  if (light === 'success') return 'success';
  if (light === 'pending') return 'warning';
  if (light === 'failed') return 'error';
  return 'default';
}

export function backupRunTrafficLightRowClass(
  status: number | undefined,
  styles: { rowSuccess: string; rowPending: string; rowFailed: string }
): string {
  const light = resolveBackupRunTrafficLight(status);
  if (light === 'success') return styles.rowSuccess;
  if (light === 'pending') return styles.rowPending;
  if (light === 'failed') return styles.rowFailed;
  return '';
}

export function isBackupRunPendingStatus(status: number | undefined): boolean {
  return (
    status === BackupRunStatus.NUMBER_0 ||
    status === BackupRunStatus.NUMBER_1 ||
    status === BackupRunStatus.NUMBER_2
  );
}
