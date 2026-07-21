/**
 * Presentation helpers for restore history (manual restore requests).
 */
import type { RestoreRequestStatusDto } from '@/features/backup-dr/logic/manualRestoreApi';

/** Prefer approval/completion time when present; else request time. */
export function restoreHistoryDisplayDate(row: RestoreRequestStatusDto): string | null {
  return row.approvedAt || row.requestedAt || null;
}

export function restoreHistoryStatusLabelKey(status: string): string {
  const normalized =
    status === 'PendingApproval'
      ? 'pendingApproval'
      : status.charAt(0).toLowerCase() + status.slice(1);
  return `backupDr.manualRestore.status.${normalized}`;
}
