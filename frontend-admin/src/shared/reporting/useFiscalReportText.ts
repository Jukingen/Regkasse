'use client';

import { useCallback, useMemo } from 'react';
import { useI18n } from '@/i18n';
import {
  fiscalReportFieldTooltip,
  resolveFiscalReportBackendText,
  type FiscalReportResolvedText,
} from '@/shared/backendLocale';
import {
  preferredReportContentLanguage,
  type ReportContentLanguage,
} from '@/shared/reporting/reportContentLanguagePolicy';

/**
 * Hooks `TextLocale` to formal report content resolution (`ReportContentLanguage`: de | en only).
 * General UI strings stay on `useI18n().t`; server-provided formal prose uses `resolveFiscal` / `resolveFormalReportBackendText`.
 */
export function useFiscalReportText() {
  const { t, textLocale } = useI18n();

  const reportContentLanguage = useMemo(
    (): ReportContentLanguage => preferredReportContentLanguage(textLocale),
    [textLocale],
  );

  const fiscalTooltip = useCallback(
    (contentLang: ReportContentLanguage) => fiscalReportFieldTooltip(t, contentLang),
    [t],
  );

  const resolveFiscal = useCallback(
    (textDe?: string | null, textEn?: string | null): FiscalReportResolvedText | undefined =>
      resolveFiscalReportBackendText(textDe, textLocale, textEn),
    [textLocale],
  );

  const resolveFormalReportBackendText = resolveFiscal;

  return {
    textLocale,
    reportContentLanguage,
    fiscalTooltip,
    resolveFiscal,
    resolveFormalReportBackendText,
  };
}
