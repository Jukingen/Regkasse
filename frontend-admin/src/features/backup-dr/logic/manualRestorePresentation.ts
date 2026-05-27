import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';

const TARGET_PREFIX = 'restore_validation_';

export function defaultValidationDatabaseName(now = new Date()): string {
  const y = now.getUTCFullYear();
  const m = String(now.getUTCMonth() + 1).padStart(2, '0');
  const d = String(now.getUTCDate()).padStart(2, '0');
  return `${TARGET_PREFIX}${y}${m}${d}`;
}

export function isValidValidationDatabaseName(name: string): boolean {
  const n = name.trim().toLowerCase();
  if (!n.startsWith(TARGET_PREFIX)) return false;
  return /^[a-z_][a-z0-9_]{0,62}$/.test(n);
}

export function isBackupRunEligibleForManualRestore(status: number | undefined): boolean {
  return status === BackupRunStatus.NUMBER_3;
}

export function isManualRestoreTerminalStatus(status: string | undefined): boolean {
  const s = (status ?? '').toLowerCase();
  return s === 'completed' || s === 'failed' || s === 'rejected';
}

export function shouldPollManualRestoreStatus(status: string | undefined): boolean {
  const s = (status ?? '').toLowerCase();
  return s === 'executing' || s === 'approved';
}

export function manualRestoreStatusTagColor(status: string | undefined): string {
  const s = (status ?? '').toLowerCase();
  if (s === 'completed') return 'success';
  if (s === 'failed' || s === 'rejected') return 'error';
  if (s === 'executing' || s === 'approved') return 'processing';
  if (s === 'pendingapproval') return 'warning';
  return 'default';
}
