'use client';

import { Select, Space, Typography } from 'antd';
import { useI18n } from '@/i18n';
import type { TextLocale } from '@/i18n';

const LANGUAGE_OPTIONS: Array<{ value: TextLocale; label: string }> = [
  { value: 'de', label: 'Deutsch' },
  { value: 'en', label: 'English' },
  { value: 'tr', label: 'Türkçe' },
];

export function LanguageSelector() {
  const { t, textLocale, setTextLocale } = useI18n();

  return (
    <Space direction="vertical" size={4} style={{ width: '100%' }}>
      <Typography.Text strong>{t('settings.language.label')}</Typography.Text>
      <Select<TextLocale>
        value={textLocale}
        onChange={(value) => setTextLocale(value)}
        options={LANGUAGE_OPTIONS}
        style={{ width: 260, maxWidth: '100%' }}
      />
    </Space>
  );
}
