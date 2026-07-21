'use client';

import { Card, Typography } from 'antd';

import { LanguageSelector } from '@/features/settings/components/LanguageSelector';
import { AppearanceSettings } from '@/features/settings/pages/AppearanceSettings';
import { useI18n } from '@/i18n';

export function PersonalizationSettings() {
  const { t } = useI18n();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <AppearanceSettings />

      <Card title={t('settings.language.cardTitle')}>
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          {t('settings.personalization.language.description')}
        </Typography.Paragraph>
        <LanguageSelector />
      </Card>
    </div>
  );
}
