'use client';

/**
 * Resolve latest verification timestamp / status from a backup run DTO.
 */
import type { BackupRunResponseDto, BackupVerificationResponseDto } from '@/api/generated/model';
import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import { BackupVerificationStatus } from '@/api/generated/model/backupVerificationStatus';

export function resolveLatestVerification(
  run: BackupRunResponseDto | null | undefined
): BackupVerificationResponseDto | null {
  const list = run?.verifications ?? [];
  if (!list.length) return null;
  return [...list].sort((a, b) => {
    const at = Date.parse(a.completedAt ?? a.startedAt ?? '') || 0;
    const bt = Date.parse(b.completedAt ?? b.startedAt ?? '') || 0;
    return bt - at;
  })[0] ?? null;
}

export function isVerificationPassed(status: number | undefined): boolean {
  return status === BackupVerificationStatus.NUMBER_1 || status === 1;
}

export function isVerificationFailed(status: number | undefined): boolean {
  return status === BackupVerificationStatus.NUMBER_2 || status === 2;
}

/**
 * List/detail Verify action: Succeeded runs, settings.manage.
 * Re-verify is allowed after an automatic Passed checksum (same as 24h re-verify).
 */
export function canShowManualVerifyAction(
  run: BackupRunResponseDto | null | undefined,
  canManageSettings: boolean
): boolean {
  if (!canManageSettings || !run?.id) return false;
  const status = run.status;
  return status === 3 || status === BackupRunStatus.NUMBER_3;
}
