/** Mandant license grace-period policy — keep in sync with backend `LicenseGracePeriodConfig`. */
export const TENANT_GRACE_PERIOD_DAYS = 7;
export const TENANT_WARNING_DAYS_BEFORE_EXPIRY = 14;
/** Days after expiry before Archived lifecycle (keep in sync with FA / backend). */
export const TENANT_ARCHIVE_AFTER_DAYS = 30;

/** Auto-open POS grace modal when remaining grace days are at or below this value. */
export const POS_GRACE_MODAL_AUTO_SHOW_DAYS = 5;

/** Escalate POS grace banner/modal styling when remaining grace days are at or below this. */
export const POS_GRACE_URGENT_DAYS = 2;
