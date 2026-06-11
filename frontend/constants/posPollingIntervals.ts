/** Shared POS background poll cadence (reduces /health, license, readiness, settings traffic). */
export const POS_HEALTH_POLL_MS = 5 * 60 * 1000;
/** Deployment license module cache + background refresh cadence. */
export const POS_LICENSE_CACHE_MS = 60 * 60 * 1000;
export const POS_LICENSE_POLL_MS = POS_LICENSE_CACHE_MS;
export const POS_MANDANT_LICENSE_POLL_MS = 5 * 60 * 1000;
export const POS_TSE_HEALTH_POLL_MS = 5 * 60 * 1000;
export const POS_ENSURE_READY_POLL_MS = 5 * 60 * 1000;
export const POS_DEVELOPMENT_MODE_POLL_MS = 5 * 60 * 1000;
