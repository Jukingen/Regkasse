import type { TextLocale } from '@/i18n/config';
import type { ReportContentLanguage } from '@/shared/reporting/reportContentLanguagePolicy';

export type { ReportContentLanguage } from '@/shared/reporting/reportContentLanguagePolicy';

/**
 * Formal fiscal / accounting report text from the API: never rendered as Turkish.
 * UI chrome may still be tr; this layer only picks German vs English for server-provided prose.
 *
 * Policy (deterministic):
 * - UI `de` → prefer German (`*De`); if missing, optional English.
 * - UI `en` → prefer optional English; if missing, German.
 * - UI `tr` (or other) → never Turkish for this content: prefer German, then English.
 *
 * @see {@link ReportContentLanguage} in `@/shared/reporting/reportContentLanguagePolicy`
 * @see `@/shared/reporting/formalReportContentResolver` — canonical re-exports for reporting UI
 */

/** @deprecated Use `ReportContentLanguage` from `@/shared/reporting/reportContentLanguagePolicy`. */
export type FiscalReportContentLang = ReportContentLanguage;

export type FiscalReportResolvedText = {
  text: string;
  /** Language of the displayed body text (never `tr`). */
  contentLang: ReportContentLanguage;
};

export function resolveFiscalReportBackendText(
  textDe: string | null | undefined,
  locale: TextLocale,
  textEn?: string | null | undefined,
): FiscalReportResolvedText | undefined {
  const de = (textDe ?? '').trim();
  const en = (textEn ?? '').trim();
  if (!de && !en) return undefined;

  if (locale === 'de') {
    if (de) return { text: de, contentLang: 'de' };
    if (en) return { text: en, contentLang: 'en' };
    return undefined;
  }

  if (locale === 'en') {
    if (en) return { text: en, contentLang: 'en' };
    if (de) return { text: de, contentLang: 'de' };
    return undefined;
  }

  // tr and any other UI locale: authoritative German for AT fiscal copy, then English
  if (de) return { text: de, contentLang: 'de' };
  if (en) return { text: en, contentLang: 'en' };
  return undefined;
}

const DEFAULT_REMEDIATION_SEPARATOR = ' | ';

/**
 * Joins server-only German remediation lines using the same resolution rules per line,
 * then merges with a stable separator. Tooltip language follows the first non-empty line.
 */
export function joinFiscalReportRemediationHints(
  hints: readonly string[] | null | undefined,
  locale: TextLocale,
  separator: string = DEFAULT_REMEDIATION_SEPARATOR,
): FiscalReportResolvedText | undefined {
  if (!hints?.length) return undefined;
  const resolved: FiscalReportResolvedText[] = [];
  for (const h of hints) {
    const r = resolveFiscalReportBackendText(h, locale);
    if (r) resolved.push(r);
  }
  if (resolved.length === 0) return undefined;
  return {
    text: resolved.map((x) => x.text).join(separator),
    contentLang: resolved[0].contentLang,
  };
}

export type FiscalExportProfileBackendRow = {
  profileKey: string;
  labelDe: string;
  descriptionDe: string;
  labelEn?: string | null;
  descriptionEn?: string | null;
  includeTraceIds: boolean;
  nonLegalOutput?: boolean;
  isDiagnosticOnly?: boolean;
};

/**
 * Resolves export profile label + description; each field may independently fall back (e.g. EN UI + EN label only).
 */
export function resolveFiscalExportProfileRow(
  p: FiscalExportProfileBackendRow,
  locale: TextLocale,
): { label: FiscalReportResolvedText; description: FiscalReportResolvedText } | undefined {
  const label = resolveFiscalReportBackendText(p.labelDe, locale, p.labelEn);
  const description = resolveFiscalReportBackendText(p.descriptionDe, locale, p.descriptionEn);
  if (!label || !description) return undefined;
  return { label, description };
}

export type LegalExportCompletenessIssueText = {
  messageDe: string;
  messageEn?: string | null;
};

/**
 * Same dual-field resolution as formal report body text; **not** `pickDualLocaleMessage` (general UI).
 */
export function resolveLegalExportCompletenessIssueMessage(
  issue: LegalExportCompletenessIssueText,
  locale: TextLocale,
): string {
  const r = resolveFiscalReportBackendText(issue.messageDe, locale, issue.messageEn);
  return r?.text ?? '';
}

export function fiscalReportFieldTooltip(
  t: (key: string, options?: Record<string, string | number>) => string,
  contentLang: ReportContentLanguage,
): string {
  return contentLang === 'en'
    ? t('reporting.backend.fiscalReportTextTitleEn')
    : t('reporting.backend.fiscalReportTextTitleDe');
}
