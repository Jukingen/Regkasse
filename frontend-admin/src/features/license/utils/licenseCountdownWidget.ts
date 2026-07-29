/** Accent color for remaining / expired license countdown states. */
export function getLicenseCountdownAccentColor(isExpired: boolean, daysLeft: number): string {
  if (isExpired) return '#cf1322';
  if (daysLeft <= 7) return '#faad14';
  if (daysLeft <= 30) return '#1890ff';
  return '#52c41a';
}

/** Progress bar percent based on remaining days of a 365-day license year. */
export function getLicenseCountdownProgressPercent(isExpired: boolean, daysLeft: number): number {
  if (isExpired) return 0;
  return Math.max(0, Math.min(100, (daysLeft / 365) * 100));
}
