'use client';

import type { MenuProps } from 'antd';
import { Button, Dropdown } from 'antd';
import { useMemo } from 'react';

import { useI18n } from '@/i18n';
import { usePersonalization } from '@/lib/personalization/PersonalizationProvider';
import type { ThemeMode } from '@/lib/personalization/types';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';

const THEME_CYCLE: ThemeMode[] = ['light', 'dark', 'system'];

export function HeaderThemeQuickSwitch() {
  const { t } = useI18n();
  const { preferences, setThemeMode } = usePersonalization();

  const items: MenuProps['items'] = useMemo(
    () =>
      THEME_CYCLE.map((mode) => ({
        key: mode,
        label: t(`settings.personalization.theme.${mode}`),
        onClick: () => setThemeMode(mode),
      })),
    [setThemeMode, t]
  );

  const currentLabel = t(`settings.personalization.theme.${preferences.themeMode}`);

  return (
    <Dropdown
      menu={{ items, selectedKeys: [preferences.themeMode] }}
      trigger={['click']}
      placement="bottomRight"
      classNames={{ root: 'admin-header-dropdown' }}
      getPopupContainer={getAdminHeaderPopupContainer}
    >
      <Button
        type="default"
        size="small"
        className="admin-header-tool-btn"
        aria-label={t('settings.personalization.theme.quickSwitchAria')}
      >
        {currentLabel}
      </Button>
    </Dropdown>
  );
}
