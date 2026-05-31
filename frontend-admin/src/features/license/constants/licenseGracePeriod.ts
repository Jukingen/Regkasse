/** Mandant (tenant) license grace-period policy — keep in sync with backend `LicenseGracePeriodConfig`. */
export const TENANT_GRACE_PERIOD_DAYS = 21;
export const TENANT_WARNING_DAYS_BEFORE_EXPIRY = 14;
export const TENANT_BLOCK_AFTER_GRACE_DAYS = 0;

export const TENANT_LOCKDOWN_STARTS_AFTER_DAYS_EXPIRED =
    TENANT_GRACE_PERIOD_DAYS + TENANT_BLOCK_AFTER_GRACE_DAYS;

/** @deprecated Use TENANT_GRACE_PERIOD_DAYS */
export const TENANT_GRACE_WRITE_DAYS = TENANT_GRACE_PERIOD_DAYS;

/** @deprecated Tenant lockdown follows grace immediately; no separate read-only phase. */
export const TENANT_LOCKDOWN_DAYS = TENANT_LOCKDOWN_STARTS_AFTER_DAYS_EXPIRED;

export const DEPLOYMENT_GRACE_WRITE_DAYS = 15;
export const DEPLOYMENT_LOCKDOWN_DAYS = 60;
