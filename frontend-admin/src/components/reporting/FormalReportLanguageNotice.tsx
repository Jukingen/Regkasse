'use client';

/**
 * Server-provided formal report fields are shown only in German or English; even when the admin UI is
 * Turkish, that prose is not Turkish. General chrome still follows `useI18n` / `TextLocale`.
 */
import { Alert, Typography } from 'antd';

import { useI18n } from '@/i18n';

export function FormalReportLanguageNotice() {
  const { t } = useI18n();
  return (
    <Alert
      type="info"
      showIcon
      style={{ marginBottom: 16 }}
      title={t('reporting.policy.formalReportLanguageTitle')}
      description={t('reporting.policy.formalReportLanguageBody')}
    />
  );
}

/** Kısa satır: profil / dışa aktarma alanında formal metin politikasını hatırlatır (detay sayfaları). */
export function FormalReportProfileLanguageCue() {
  const { t } = useI18n();
  return (
    <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
      {t('reporting.policy.formalReportLanguageProfileCue')}
    </Typography.Text>
  );
}
