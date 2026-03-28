import type { TextLocale } from '@/i18n/config';

/**
 * Allowed languages for **formal report content** (API prose, legal labels, export profiles). Turkish is not
 * a formal-report content language; it is never selected for that layer.
 *
 * Dual-field resolution (`resolveFormalReportContentFromDualFields` in `formalReportContentResolver`) applies:
 * - UI `de` → prefer German, then English if German missing
 * - UI `en` → prefer English, then German if English missing
 * - UI `tr` → never Turkish for content: prefer German, then English
 *
 * @see `../backendLocale/fiscalReportTextPolicy` — implementation
 * @see `./formalReportContentResolver` — canonical imports for reporting features
 */
export const FORMAL_REPORT_CONTENT_POLICY_VERSION = 1 as const;

export const FORMAL_REPORT_ALLOWED_CONTENT_LANGUAGES = ['de', 'en'] as const;
export type ReportContentLanguage = (typeof FORMAL_REPORT_ALLOWED_CONTENT_LANGUAGES)[number];

/** True only for `de` or `en` (e.g. `tr` is never a formal content language). */
export function isReportContentLanguage(value: unknown): value is ReportContentLanguage {
  return value === 'de' || value === 'en';
}

/**
 * Preferred formal content language when both DE and EN exist and the UI needs a single code (e.g. tooltips).
 * Does **not** replace dual-field resolution for actual strings — use `resolveFormalReportContentFromDualFields`.
 */
export function preferredReportContentLanguage(uiLocale: TextLocale): ReportContentLanguage {
  return uiLocale === 'en' ? 'en' : 'de';
}
