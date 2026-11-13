export type LimitUsageTone = 'ok' | 'warning' | 'exceeded';

export function limitUsagePercent(current: number, limit: number): number {
  if (!Number.isFinite(current) || !Number.isFinite(limit) || limit <= 0) return 0;
  return Math.min(999, Math.round((current / limit) * 100));
}

export function limitUsageTone(percent: number, warningThreshold = 80): LimitUsageTone {
  if (percent >= 100) return 'exceeded';
  if (percent >= warningThreshold) return 'warning';
  return 'ok';
}

export function limitProgressStatus(
  tone: LimitUsageTone
): 'success' | 'normal' | 'exception' {
  if (tone === 'exceeded') return 'exception';
  if (tone === 'warning') return 'normal';
  return 'success';
}

export function limitProgressStroke(tone: LimitUsageTone): string | undefined {
  if (tone === 'warning') return '#fa8c16';
  return undefined;
}
