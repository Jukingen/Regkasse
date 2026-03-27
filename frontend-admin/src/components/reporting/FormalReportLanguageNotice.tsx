'use client';

/**
 * Formel rapor alanlarında sunucu metni yalnızca Almanca veya İngilizce gösterilir; `TextLocale` Türkçe olsa bile
 * resmi metin Türkçe değildir. Genel arayüz çevirisi `useI18n` ile ayrı kalır.
 */
import { Alert } from 'antd';
import { useI18n } from '@/i18n';

export function FormalReportLanguageNotice() {
  const { t } = useI18n();
  return (
    <Alert
      type="info"
      showIcon
      style={{ marginBottom: 16 }}
      message={t('reporting.policy.formalReportLanguageBanner')}
    />
  );
}
