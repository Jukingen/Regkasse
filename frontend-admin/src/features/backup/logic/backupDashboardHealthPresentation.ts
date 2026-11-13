/**
 * Maps backup dashboard stats/health into widget presentation (score bands + labels).
 */
export type BackupHealthLevel = 'healthy' | 'warning' | 'critical';
export type BackupRpoDisplay = 'Healthy' | 'AtRisk' | 'Critical' | 'Unknown';
export type BackupVerificationDisplay = 'Passed' | 'Failed' | 'None';
export type BackupContentDisplay =
  | 'passed'
  | 'failed'
  | 'partial'
  | 'unavailable'
  | 'unknown';

export interface BackupDashboardHealthViewModel {
  healthScore: number;
  healthLevel: BackupHealthLevel;
  healthEmoji: '🟢' | '🟡' | '🔴';
  verificationStatus: BackupVerificationDisplay;
  contentValidationStatus: BackupContentDisplay;
  rpoStatus: BackupRpoDisplay;
  rpoHours: number | null;
}

export function healthLevelFromScore(score: number): BackupHealthLevel {
  if (score >= 80) return 'healthy';
  if (score >= 50) return 'warning';
  return 'critical';
}

export function healthEmojiForLevel(level: BackupHealthLevel): '🟢' | '🟡' | '🔴' {
  if (level === 'healthy') return '🟢';
  if (level === 'critical') return '🔴';
  return '🟡';
}

export function normalizeRpoStatus(status: string | null | undefined): BackupRpoDisplay {
  const s = (status ?? '').trim().toLowerCase();
  if (s === 'healthy' || s === 'ok') return 'Healthy';
  if (s === 'atrisk' || s === 'at_risk' || s === 'warning') return 'AtRisk';
  if (s === 'critical' || s === 'overdue') return 'Critical';
  return 'Unknown';
}

export function normalizeVerificationStatus(
  status: number | string | null | undefined
): BackupVerificationDisplay {
  if (status === 1 || status === 'Passed' || status === 'passed') return 'Passed';
  if (status === 2 || status === 'Failed' || status === 'failed') return 'Failed';
  return 'None';
}

export function normalizeContentValidationStatus(
  status: string | null | undefined
): BackupContentDisplay {
  const s = (status ?? '').trim().toLowerCase();
  if (s === 'passed') return 'passed';
  if (s === 'failed') return 'failed';
  if (s === 'partial' || s === 'warning') return 'partial';
  if (s === 'unavailable') return 'unavailable';
  return 'unknown';
}

export function mapBackupDashboardHealth(input: {
  healthScore?: number | null;
  healthLevel?: string | null;
  lastVerificationStatus?: number | string | null;
  verificationStatus?: string | null;
  contentValidationStatus?: string | null;
  contentValidationSummaryStatus?: string | null;
  rpoStatus?: string | null;
  rpoHours?: number | null;
}): BackupDashboardHealthViewModel {
  const score = Math.max(0, Math.min(100, Math.round(input.healthScore ?? 0)));
  const fromScore = healthLevelFromScore(score);
  const rawLevel = (input.healthLevel ?? '').trim().toLowerCase();
  const healthLevel: BackupHealthLevel =
    rawLevel === 'healthy' || rawLevel === 'warning' || rawLevel === 'critical'
      ? rawLevel
      : fromScore;

  return {
    healthScore: score,
    healthLevel,
    healthEmoji: healthEmojiForLevel(healthLevel),
    verificationStatus: normalizeVerificationStatus(
      input.verificationStatus ?? input.lastVerificationStatus
    ),
    contentValidationStatus: normalizeContentValidationStatus(
      input.contentValidationStatus ?? input.contentValidationSummaryStatus
    ),
    rpoStatus: normalizeRpoStatus(input.rpoStatus),
    rpoHours: input.rpoHours ?? null,
  };
}
