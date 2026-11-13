/**
 * FA monitoring SLOs / alert thresholds (client + docs stay in sync).
 */

/** Rolling client API error rate above this → alert (fraction 0–1). */
export const API_ERROR_RATE_ALERT_THRESHOLD = 0.05; // 5%

/** Single API call slower than this → slow-request signal (ms). */
export const API_RESPONSE_TIME_ALERT_MS = 1000; // 1s

/** Minimum samples before error-rate alert can fire. */
export const API_ERROR_RATE_MIN_SAMPLES = 20;

/** Rolling window for client metrics (ms). */
export const API_METRICS_WINDOW_MS = 5 * 60 * 1000; // 5 minutes

/** Rate-limit identical alert messages (ms). */
export const ALERT_COOLDOWN_MS = 5 * 60 * 1000;

export const MONITORING_THRESHOLDS = {
  apiErrorRate: API_ERROR_RATE_ALERT_THRESHOLD,
  apiResponseTimeMs: API_RESPONSE_TIME_ALERT_MS,
  apiErrorRateMinSamples: API_ERROR_RATE_MIN_SAMPLES,
  metricsWindowMs: API_METRICS_WINDOW_MS,
  alertCooldownMs: ALERT_COOLDOWN_MS,
} as const;
