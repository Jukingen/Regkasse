'use client';

import { useCallback, useMemo } from 'react';

import { useI18n } from '@/i18n';
import {
  type FiscalExportProfileBackendRow,
  type FiscalReportResolvedText,
  type LegalExportCompletenessIssueText,
  formalReportBackendFieldTooltip,
  joinFormalReportRemediationHints,
  resolveFormalReportContentFromDualFields,
  resolveFormalReportExportProfileRow,
  resolveFormalReportLegalExportIssueMessage,
} from '@/shared/reporting/formalReportContentResolver';
import {
  type ReportContentLanguage,
  preferredReportContentLanguage,
} from '@/shared/reporting/reportContentLanguagePolicy';

/**
 * Binds `TextLocale` from `useI18n` to formal report content resolution (`ReportContentLanguage`: de | en only).
 * General UI strings stay on `useI18n().t`; server-provided formal prose uses the helpers below.
 */
export function useFiscalReportText() {
  const { t, textLocale } = useI18n();

  const reportContentLanguage = useMemo(
    (): ReportContentLanguage => preferredReportContentLanguage(textLocale),
    [textLocale]
  );

  const fiscalTooltip = useCallback(
    (contentLang: ReportContentLanguage) => formalReportBackendFieldTooltip(t, contentLang),
    [t]
  );

  const resolveFiscal = useCallback(
    (textDe?: string | null, textEn?: string | null): FiscalReportResolvedText | undefined =>
      resolveFormalReportContentFromDualFields(textDe, textLocale, textEn),
    [textLocale]
  );

  const joinRemediationHints = useCallback(
    (hints: readonly string[] | null | undefined, separator?: string) =>
      joinFormalReportRemediationHints(hints, textLocale, separator),
    [textLocale]
  );

  const resolveExportProfileRow = useCallback(
    (p: FiscalExportProfileBackendRow) => resolveFormalReportExportProfileRow(p, textLocale),
    [textLocale]
  );

  const resolveLegalExportCompletenessIssue = useCallback(
    (issue: LegalExportCompletenessIssueText) =>
      resolveFormalReportLegalExportIssueMessage(issue, textLocale),
    [textLocale]
  );

  return {
    textLocale,
    reportContentLanguage,
    fiscalTooltip,
    resolveFiscal,
    resolveFormalReportContentFromDualFields: resolveFiscal,
    joinRemediationHints,
    resolveExportProfileRow,
    resolveLegalExportCompletenessIssue,
    resolveFormalReportBackendText: resolveFiscal,
  };
}
