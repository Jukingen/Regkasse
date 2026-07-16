export type MandantLicenseWarningState = {
  daysRemaining: number;
  gracePeriodRemaining: number;
  isInGracePeriod: boolean;
  canAccess: boolean;
  statusMessage?: string | null;
  /** Full expiry timestamp from license status (not date-only). */
  validUntil?: string | null;
};
