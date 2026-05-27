'use client';

import {
  ColumnWidthOutlined,
  CompressOutlined,
  ExpandOutlined,
  MobileOutlined,
  MoonOutlined,
  SunOutlined,
} from '@ant-design/icons';
import { Card, Form, Radio, Select, Switch, Typography } from 'antd';
import { useMemo } from 'react';
import { useDensity } from '@/hooks/useDensity';
import { useTheme } from '@/hooks/useTheme';
import { useI18n } from '@/i18n';
import { useUserPreferences } from '@/lib/personalization/hooks/useUserPreferences';
import { DATE_FORMAT_PATTERNS, DEFAULT_LANDING_PATHS } from '@/lib/personalization/types';
import type { DensityMode, ThemeMode } from '@/lib/personalization/types';

const { Option } = Select;

/** Appearance preferences: theme, density, landing page, formats, reduced motion. */
export function AppearanceSettings() {
  const { t } = useI18n();
  const { themeMode, setThemeMode } = useTheme();
  const { densityMode, setDensityMode } = useDensity();
  const { preferences, updatePreferences, isSyncing } = useUserPreferences();

  const landingOptions = useMemo(
    () =>
      DEFAULT_LANDING_PATHS.map((path) => ({
        value: path,
        label: t(`settings.personalization.landing.${path.replace(/^\//, '').replace(/\//g, '_')}`),
      })),
    [t],
  );

  const dateFormatOptions = useMemo(
    () =>
      DATE_FORMAT_PATTERNS.map((pattern) => ({
        value: pattern,
        label: t(
          `settings.personalization.appearance.dateFormatOptions.${pattern.replace(/\./g, '_').replace(/\//g, '_')}`,
        ),
      })),
    [t],
  );

  return (
    <Card title={t('settings.personalization.appearance.cardTitle')}>
      <Form layout="vertical" className="appearance-settings-form">
        <Form.Item label={t('settings.personalization.appearance.theme')}>
          <Radio.Group
            value={themeMode}
            onChange={(e) => setThemeMode(e.target.value as ThemeMode)}
          >
            <Radio.Button value="light">
              <SunOutlined /> {t('settings.personalization.theme.light')}
            </Radio.Button>
            <Radio.Button value="dark">
              <MoonOutlined /> {t('settings.personalization.theme.dark')}
            </Radio.Button>
            <Radio.Button value="system">
              <MobileOutlined /> {t('settings.personalization.theme.system')}
            </Radio.Button>
          </Radio.Group>
        </Form.Item>

        <Form.Item label={t('settings.personalization.appearance.density')}>
          <Radio.Group
            value={densityMode}
            onChange={(e) => setDensityMode(e.target.value as DensityMode)}
          >
            <Radio.Button value="comfortable">
              <ExpandOutlined /> {t('settings.personalization.density.comfortable')}
            </Radio.Button>
            <Radio.Button value="standard">
              <ColumnWidthOutlined /> {t('settings.personalization.density.standard')}
            </Radio.Button>
            <Radio.Button value="compact">
              <CompressOutlined /> {t('settings.personalization.density.compact')}
            </Radio.Button>
          </Radio.Group>
        </Form.Item>

        <Form.Item label={t('settings.personalization.appearance.defaultPage')}>
          <Select
            value={preferences.defaultPage}
            onChange={(value) => updatePreferences({ defaultPage: value })}
            style={{ maxWidth: 360, width: '100%' }}
          >
            {landingOptions.map((opt) => (
              <Option key={opt.value} value={opt.value}>
                {opt.label}
              </Option>
            ))}
          </Select>
        </Form.Item>

        <Form.Item label={t('settings.personalization.appearance.dateFormatLabel')}>
          <Select
            value={preferences.dateFormat}
            onChange={(value) => updatePreferences({ dateFormat: value })}
            style={{ maxWidth: 360, width: '100%' }}
          >
            {dateFormatOptions.map((opt) => (
              <Option key={opt.value} value={opt.value}>
                {opt.label}
              </Option>
            ))}
          </Select>
        </Form.Item>

        <Form.Item label={t('settings.personalization.appearance.timeFormat')}>
          <Radio.Group
            value={preferences.timeFormat}
            onChange={(e) => updatePreferences({ timeFormat: e.target.value })}
          >
            <Radio.Button value="24h">{t('settings.personalization.timeFormat.h24Example')}</Radio.Button>
            <Radio.Button value="12h">{t('settings.personalization.timeFormat.h12Example')}</Radio.Button>
          </Radio.Group>
        </Form.Item>

        <Form.Item label={t('settings.personalization.appearance.reducedMotion')}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <Switch
              checked={preferences.reducedAnimations}
              onChange={(checked) => updatePreferences({ reducedAnimations: checked })}
              aria-label={t('settings.personalization.appearance.reducedMotion')}
            />
            <Typography.Text type="secondary">
              {t('settings.personalization.appearance.reducedMotionHint')}
            </Typography.Text>
          </div>
        </Form.Item>
      </Form>

      {isSyncing ? (
        <Typography.Text type="secondary">{t('settings.personalization.syncing')}</Typography.Text>
      ) : null}
    </Card>
  );
}
