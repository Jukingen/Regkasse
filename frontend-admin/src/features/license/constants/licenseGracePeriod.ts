/** Mandant (tenant) license grace-period policy — keep in sync with backend `LicenseGracePeriodConfig`. */
export const TENANT_GRACE_PERIOD_DAYS = 7;
export const TENANT_WARNING_DAYS_BEFORE_EXPIRY = 14;
export const TENANT_BLOCK_AFTER_GRACE_DAYS = 0;
/** Days overdue after which Locked becomes Archived (POS blocked, FA read-only). */
export const TENANT_ARCHIVE_AFTER_DAYS = 30;

export const TENANT_LOCKDOWN_STARTS_AFTER_DAYS_EXPIRED =
  TENANT_GRACE_PERIOD_DAYS + TENANT_BLOCK_AFTER_GRACE_DAYS;

const DAY_MS = 24 * 60 * 60 * 1000;

/** Clamp API/UI grace remaining to the real 7-day window (never a ValidUntil horizon). */
export function clampTenantGraceRemaining(value: number): number {
  if (!Number.isFinite(value)) return 0;
  return Math.max(0, Math.min(TENANT_GRACE_PERIOD_DAYS, Math.trunc(value)));
}

export type TenantGraceDays = {
  daysExpired: number;
  graceRemaining: number;
};

/**
 * Resolve overdue + remaining grace days.
 * Never treats a future `validUntilUtc` (e.g. 2029 → ~997 days) as grace remaining.
 */
export function resolveTenantGraceDays(input: {
  daysRemaining?: number | null;
  gracePeriodRemaining?: number | null;
  validUntilUtc?: string | null;
  nowMs?: number;
}): TenantGraceDays {
  const nowMs = input.nowMs ?? Date.now();

  if (
    typeof input.gracePeriodRemaining === 'number' &&
    Number.isFinite(input.gracePeriodRemaining)
  ) {
    const graceRemaining = clampTenantGraceRemaining(input.gracePeriodRemaining);
    return {
      daysExpired: Math.max(0, TENANT_GRACE_PERIOD_DAYS - graceRemaining),
      graceRemaining,
    };
  }

  if (
    typeof input.daysRemaining === 'number' &&
    Number.isFinite(input.daysRemaining) &&
    input.daysRemaining < 0
  ) {
    const daysExpired = Math.abs(Math.trunc(input.daysRemaining));
    return {
      daysExpired,
      graceRemaining: clampTenantGraceRemaining(TENANT_GRACE_PERIOD_DAYS - daysExpired),
    };
  }

  // Only derive from ValidUntil when expiry is already in the past.
  if (input.validUntilUtc?.trim()) {
    const expiresAtMs = new Date(input.validUntilUtc).getTime();
    if (Number.isFinite(expiresAtMs) && expiresAtMs < nowMs) {
      const daysExpired = Math.max(0, Math.floor((nowMs - expiresAtMs) / DAY_MS));
      return {
        daysExpired,
        graceRemaining: clampTenantGraceRemaining(TENANT_GRACE_PERIOD_DAYS - daysExpired),
      };
    }
  }

  return { daysExpired: 0, graceRemaining: 0 };
}

/** @deprecated Use TENANT_GRACE_PERIOD_DAYS */
export const TENANT_GRACE_WRITE_DAYS = TENANT_GRACE_PERIOD_DAYS;

/** @deprecated Prefer TENANT_LOCKDOWN_STARTS_AFTER_DAYS_EXPIRED / TENANT_ARCHIVE_AFTER_DAYS. */
export const TENANT_LOCKDOWN_DAYS = TENANT_LOCKDOWN_STARTS_AFTER_DAYS_EXPIRED;

export const DEPLOYMENT_GRACE_WRITE_DAYS = 15;
export const DEPLOYMENT_LOCKDOWN_DAYS = 60;
