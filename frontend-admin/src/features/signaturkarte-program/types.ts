export type SignaturkarteProgramTotals = {
  compliant: number;
  nonCompliant: number;
  excluded: number;
  revoked: number;
  total: number;
};

export type SignaturkarteProgramStatus = {
  enabled: boolean;
  displayName: string;
  deadlineUtc: string;
  daysRemaining: number;
  bannerSeverity: 'info' | 'warning' | 'critical' | null;
  totals: SignaturkarteProgramTotals;
  milestonesNext: number | null;
  isCertificateExpiry: boolean;
  separationNote: string;
};

export type SignaturkarteProgramDevice = {
  deviceId: string;
  tenantId: string | null;
  tenantSlug: string | null;
  tenantName: string | null;
  serialNumber: string;
  provider: string | null;
  deviceType: string | null;
  certificateStatus: string | null;
  expiresAt: string | null;
  programCompliantAtUtc: string | null;
  programCompliantBy: string | null;
  programNote: string | null;
  status: 'Compliant' | 'Open' | 'Excluded' | 'Revoked' | string;
  daysToDeadline: number;
  certificateExpiresBeforeDeadline: boolean;
};

export type SignaturkarteProgramMarkCompliantResponse = {
  success: boolean;
  deviceId: string;
  compliantAtUtc?: string;
  message?: string | null;
};
