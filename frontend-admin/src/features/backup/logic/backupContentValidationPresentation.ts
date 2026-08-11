import type { BackupRunResponseDto, BackupVerificationResponseDto } from '@/api/generated/model';
import { normalizeContentValidationStatus } from '@/features/backup/logic/backupContentValidationApi';

const CONTENT_VERIFIER = 'content_validation';

export function resolveLatestContentVerification(
  run: BackupRunResponseDto | null | undefined
): BackupVerificationResponseDto | null {
  const list = (run?.verifications ?? []).filter(
    (v) => (v.verifierSource ?? '').trim().toLowerCase() === CONTENT_VERIFIER
  );
  if (!list.length) return null;
  return [...list].sort((a, b) => {
    const aAt = Date.parse(a.completedAt ?? a.startedAt ?? '') || 0;
    const bAt = Date.parse(b.completedAt ?? b.startedAt ?? '') || 0;
    return bAt - aAt;
  })[0]!;
}

export function contentStatusFromVerification(
  verification: BackupVerificationResponseDto | null | undefined
): string | null {
  if (!verification) return null;
  // Passed verifications may be Partial overall — API DetailsJson is not on list DTO.
  // Map binary verification status; session/report override supplies Passed/Partial/Failed text.
  if (verification.status === 1) return 'Passed';
  if (verification.status === 2) return 'Failed';
  return null;
}

export function resolveContentValidationBadgeStatus(
  run: BackupRunResponseDto | null | undefined,
  sessionStatus?: string | null
): string | null {
  if (sessionStatus) return sessionStatus;
  return contentStatusFromVerification(resolveLatestContentVerification(run));
}

export function isContentValidationPositive(status: string | null | undefined): boolean {
  return normalizeContentValidationStatus(status) === 'passed';
}
