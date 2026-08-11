/**
 * License validity health bands for FA sales tables (Tag colors + tooltips).
 *
 * Adjust these values when business rules change — no UI rewrites required.
 * Optional `NEXT_PUBLIC_LICENSE_*` env overrides (Development / deploy-time only).
 *
 * Bands (evaluated in order):
 * - longTerm  → gray (`default`): remaining > longTermDays (~2 years)
 * - healthy   → green (`success`): remaining > healthyAfterDays (30)
 * - warning   → yellow (`gold`): remaining > criticalThroughDays (7) and ≤ 30
 * - critical  → orange: remaining 0 … criticalThroughDays (ends today … 7 days)
 * - expired   → red (`error`): remaining < 0
 *
 * Display-only “unlimited” label uses unlimitedAfterDays (~5 years), separate from gray band.
 */

function readPublicInt(envName: string, fallback: number): number {
  if (typeof process === 'undefined') return fallback;
  const raw = process.env[envName];
  if (raw == null || raw.trim() === '') return fallback;
  const parsed = Number.parseInt(raw, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

/** Calendar days remaining above this → “Süresiz” / Unlimited label (not a health color). */
export const LICENSE_DAYS_UNLIMITED_THRESHOLD = readPublicInt(
  'NEXT_PUBLIC_LICENSE_UNLIMITED_AFTER_DAYS',
  5 * 365
);

/**
 * Calendar days remaining above this → long-term / gray Tag.
 * Default: 2 years (730 days).
 */
export const LICENSE_DAYS_LONG_TERM_THRESHOLD = readPublicInt(
  'NEXT_PUBLIC_LICENSE_LONG_TERM_AFTER_DAYS',
  2 * 365
);

/**
 * Remaining days must be strictly greater than this for green / healthy.
 * Default: 30 → green when days > 30.
 */
export const LICENSE_DAYS_HEALTHY_AFTER = readPublicInt(
  'NEXT_PUBLIC_LICENSE_HEALTHY_AFTER_DAYS',
  30
);

/**
 * Inclusive upper bound for orange / critical (0 … N days left, including “ends today”).
 * Days from (N+1) through healthyAfter are yellow / warning.
 * Default: 7.
 */
export const LICENSE_DAYS_CRITICAL_THROUGH = readPublicInt(
  'NEXT_PUBLIC_LICENSE_CRITICAL_THROUGH_DAYS',
  7
);

/** Whole years for tooltip copy (from long-term threshold). */
export function licenseLongTermThresholdYears(): number {
  return Math.max(1, Math.round(LICENSE_DAYS_LONG_TERM_THRESHOLD / 365));
}

export const LICENSE_VALIDITY_THRESHOLDS = {
  unlimitedAfterDays: LICENSE_DAYS_UNLIMITED_THRESHOLD,
  longTermAfterDays: LICENSE_DAYS_LONG_TERM_THRESHOLD,
  healthyAfterDays: LICENSE_DAYS_HEALTHY_AFTER,
  criticalThroughDays: LICENSE_DAYS_CRITICAL_THROUGH,
} as const;
