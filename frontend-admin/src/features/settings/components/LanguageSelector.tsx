'use client';

/**
 * Settings page UI language: same `setTextLocale` + persistence as `HeaderLanguageQuickSwitch`.
 */
import { useMemo } from 'react';
import { Select, Space, Typography } from 'antd';
import { useI18n } from '@/i18n';
import type { TextLocale } from '@/i18n';

export function LanguageSelector() {
  const { t, textLocale, setTextLocale } = useI18n();

  const languageOptions = useMemo(
    () =>
      [
        { value: 'de' as const, label: t('settings.language.localeDe') },
        { value: 'en' as const, label: t('settings.language.localeEn') },
        { value: 'tr' as const, label: t('settings.language.localeTr') },
      ] as const,
    [t],
  );

  return (
    <Space orientation="vertical" size={4} style={{ width: '100%' }}>
      <Typography.Text strong>{t('settings.language.label')}</Typography.Text>
      <Select<TextLocale>
        value={textLocale}
        onChange={(value) => setTextLocale(value)}
        options={[...languageOptions]}
        style={{ width: 260, maxWidth: '100%' }}
      />
    </Space>
  );
}
