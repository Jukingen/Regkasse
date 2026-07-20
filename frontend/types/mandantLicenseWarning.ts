export type MandantLicenseWarningState = {
  daysRemaining: number;
  daysOverdue: number;
  gracePeriodRemaining: number;
  isInGracePeriod: boolean;
  isLocked: boolean;
  canAccess: boolean;
  statusMessage?: string | null;
  lockDate?: string | null;
  restrictions?: readonly string[];
  /** Full expiry timestamp from license status (not date-only). */
  validUntil?: string | null;
};
