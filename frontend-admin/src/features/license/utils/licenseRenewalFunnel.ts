export type LicenseRenewalFunnelCounts = {
  total: number;
  reminderSent: number;
  pageViewed: number;
  renewed: number;
  activated: number;
  conversionRate: number;
};

/** Safe percent of step count vs cohort total (0–100). */
export function getLicenseRenewalFunnelStepPercent(count: number, total: number): number {
  if (!Number.isFinite(count) || !Number.isFinite(total) || total <= 0) return 0;
  return Math.min(100, Math.max(0, Math.round((count / total) * 1000) / 10));
}

export function getLicenseRenewalFunnelStrokeColor(
  step: 'reminder' | 'pageView' | 'renewed' | 'activated',
  percent: number
): string | undefined {
  switch (step) {
    case 'reminder':
      return undefined;
    case 'pageView':
      return '#1890ff';
    case 'renewed':
      return percent > 50 ? '#52c41a' : '#faad14';
    case 'activated':
      return percent > 50 ? '#52c41a' : '#cf1322';
    default:
      return undefined;
  }
}
