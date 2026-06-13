'use client';

/**
 * Settings page UI language: reuses shared `LanguageSwitcher` (same persistence as header quick switch).
 */
import { Space, Typography } from 'antd';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import { useI18n } from '@/i18n';

export function LanguageSelector() {
  const { t } = useI18n();

  return (
    <Space orientation="vertical" size={4} style={{ width: '100%' }}>
      <Typography.Text strong>{t('settings.language.label')}</Typography.Text>
      <LanguageSwitcher style={{ width: 260, maxWidth: '100%' }} data-testid="settings-language-switcher" />
    </Space>
  );
}
