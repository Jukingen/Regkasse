/**
 * Core Web Vitals thresholds and helpers for FA performance monitoring.
 *
 * Google “good” budgets (field data):
 * - LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1
 * - FCP ≤ 1.8s, TTFB ≤ 800ms (supportive)
 *
 * TTI (Time to Interactive) is deprecated in modern Core Web Vitals —
 * FA tracks **INP** (Interaction to Next Paint) as the interactivity signal.
 */
export type WebVitalName = 'CLS' | 'FCP' | 'INP' | 'LCP' | 'TTFB';

/** Legacy alias requested by ops — mapped to INP in reporting. */
export type LegacyInteractiveMetric = 'TTI';

export type WebVitalRating = 'good' | 'needs-improvement' | 'poor';

export type WebVitalPayload = {
  name: WebVitalName;
  /** Raw metric value (ms for timing metrics; unitless for CLS). */
  value: number;
  rating: WebVitalRating;
  id: string;
  navigationType?: string;
  /** Pathname only — never query/hash (PII / tenant leakage risk). */
  route: string;
  /** Optional delta since last report for the same metric id. */
  delta?: number;
};

/** Alert / SLO budgets used by reporters and monthly reviews. */
export const WEB_VITAL_BUDGETS_MS = {
  /** Largest Contentful Paint — alert when above this (ms). */
  LCP: 2500,
  /** First Contentful Paint (ms). */
  FCP: 1800,
  /** Time to First Byte (ms). */
  TTFB: 800,
  /** Interaction to Next Paint (ms) — replaces TTI. */
  INP: 200,
} as const;

/** CLS is unitless (layout shift score). */
export const WEB_VITAL_BUDGETS_CLS = {
  CLS: 0.1,
} as const;

export function isTimingVital(name: WebVitalName): boolean {
  return name === 'LCP' || name === 'FCP' || name === 'TTFB' || name === 'INP';
}

export function exceedsBudget(name: WebVitalName, value: number): boolean {
  if (name === 'CLS') {
    return value > WEB_VITAL_BUDGETS_CLS.CLS;
  }
  const budget = WEB_VITAL_BUDGETS_MS[name];
  return typeof budget === 'number' && value > budget;
}

export function budgetFor(name: WebVitalName): number {
  if (name === 'CLS') {
    return WEB_VITAL_BUDGETS_CLS.CLS;
  }
  return WEB_VITAL_BUDGETS_MS[name];
}

/** Sanitize pathname for telemetry (strip query/hash; cap length). */
export function sanitizeRoutePath(pathname: string | null | undefined): string {
  if (!pathname || typeof pathname !== 'string') {
    return '/';
  }
  const noQuery = pathname.split('?')[0]?.split('#')[0] ?? '/';
  const trimmed = noQuery.trim() || '/';
  return trimmed.length > 200 ? `${trimmed.slice(0, 200)}…` : trimmed;
}
