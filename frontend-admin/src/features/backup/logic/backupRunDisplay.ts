import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupTriggerSource } from '@/api/generated/model/backupTriggerSource';

export type BackupRunDisplayFields = {
  tenantId?: string | null;
  tenantName?: string | null;
  tenantSlug?: string | null;
  requestedByDisplayName?: string | null;
  requestedByEmail?: string | null;
  requestedByLabel?: string | null;
  requestedFromIp?: string | null;
  downloadCount?: number;
  primaryArtifactId?: string | null;
  primaryDownloadFileName?: string | null;
};

export type BackupRunWithDisplay = BackupRunResponseDto & BackupRunDisplayFields;

export function asBackupRunDisplay(run: BackupRunResponseDto | null | undefined): BackupRunWithDisplay {
  return (run ?? {}) as BackupRunWithDisplay;
}

export function isScheduledBackupActor(run: BackupRunResponseDto | BackupRunWithDisplay | undefined): boolean {
  if (!run) return false;
  const trigger = run.triggerSource;
  const userId = run.requestedByUserId?.trim();
  return trigger === BackupTriggerSource.NUMBER_1 || !userId;
}
