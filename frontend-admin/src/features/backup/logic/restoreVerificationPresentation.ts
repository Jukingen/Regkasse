import type {
  RestoreVerificationCheckResult,
  RestoreVerificationRunStatus,
  RestoreVerificationTriggerSource,
  RestoreVerificationVerdict,
} from '@/features/backup/logic/restoreVerificationApi';

export const RESTORE_VERIFICATION_CHECK_ORDER = ['hash', 'schema', 'data', 'tse', 'fiscal'] as const;

export function restoreVerificationStatusValue(status: RestoreVerificationRunStatus | undefined): number | null {
  if (status === undefined || status === null) return null;
  if (typeof status === 'number') return status;
  const map: Record<string, number> = { Queued: 0, Running: 1, Succeeded: 2, Failed: 3 };
  return map[status] ?? null;
}

export function restoreVerificationTriggerValue(
  trigger: RestoreVerificationTriggerSource | undefined
): number | null {
  if (trigger === undefined || trigger === null) return null;
  if (typeof trigger === 'number') return trigger;
  return trigger === 'Scheduled' ? 1 : trigger === 'Manual' ? 0 : null;
}

export function restoreVerificationStatusLabelKey(status: RestoreVerificationRunStatus | undefined): string {
  const n = restoreVerificationStatusValue(status);
  if (n === 0) return 'backupDr.restoreVerificationPage.status.queued';
  if (n === 1) return 'backupDr.restoreVerificationPage.status.running';
  if (n === 2) return 'backupDr.restoreVerificationPage.status.succeeded';
  if (n === 3) return 'backupDr.restoreVerificationPage.status.failed';
  return 'backupDr.restoreVerificationPage.status.unknown';
}

export function restoreVerificationStatusColor(
  status: RestoreVerificationRunStatus | undefined
): 'processing' | 'success' | 'error' | 'default' {
  const n = restoreVerificationStatusValue(status);
  if (n === 1) return 'processing';
  if (n === 2) return 'success';
  if (n === 3) return 'error';
  return 'default';
}

export function restoreVerificationTypeLabelKey(
  trigger: RestoreVerificationTriggerSource | undefined
): string {
  const n = restoreVerificationTriggerValue(trigger);
  if (n === 1) return 'backupDr.restoreVerificationPage.type.scheduled';
  return 'backupDr.restoreVerificationPage.type.manual';
}

export function restoreVerificationVerdictLabelKey(verdict: RestoreVerificationVerdict | null | undefined): string {
  if (verdict === 'passed') return 'backupDr.restoreVerificationPage.verdict.passed';
  if (verdict === 'failed') return 'backupDr.restoreVerificationPage.verdict.failed';
  return 'backupDr.restoreVerificationPage.verdict.pending';
}

export function restoreVerificationVerdictColor(
  verdict: RestoreVerificationVerdict | null | undefined
): 'success' | 'error' | 'warning' {
  if (verdict === 'passed') return 'success';
  if (verdict === 'failed') return 'error';
  return 'warning';
}

export function restoreVerificationCheckLabelKey(id: string): string {
  const known = RESTORE_VERIFICATION_CHECK_ORDER as readonly string[];
  if (known.includes(id)) return `backupDr.restoreVerificationPage.checks.${id}`;
  return 'backupDr.restoreVerificationPage.checks.unknown';
}

export function restoreVerificationCheckResultLabelKey(result: RestoreVerificationCheckResult): string {
  return `backupDr.restoreVerificationPage.checkResult.${result}`;
}

export function restoreVerificationCheckResultColor(
  result: RestoreVerificationCheckResult
): 'success' | 'error' | 'default' | 'warning' {
  if (result === 'passed') return 'success';
  if (result === 'failed') return 'error';
  if (result === 'skipped') return 'default';
  return 'warning';
}

export function restoreVerificationCheckGlyph(result: RestoreVerificationCheckResult): string {
  if (result === 'passed') return '✅';
  if (result === 'failed') return '❌';
  if (result === 'skipped') return '—';
  return '?';
}

export function triggerDownloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
