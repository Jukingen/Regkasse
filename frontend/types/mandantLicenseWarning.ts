export type MandantLicenseWarningState = {
  daysRemaining: number;
  gracePeriodRemaining: number;
  isInGracePeriod: boolean;
  canAccess: boolean;
  statusMessage?: string | null;
};
