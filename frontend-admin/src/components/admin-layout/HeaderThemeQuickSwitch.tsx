'use client';

import { Button, Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import { BgColorsOutlined } from '@ant-design/icons';
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
    [setThemeMode, t],
  );

  const ariaLabel = t('settings.personalization.theme.quickSwitchAria');

  return (
    <Dropdown
      menu={{ items, selectedKeys: [preferences.themeMode] }}
      trigger={['click']}
      placement="bottomRight"
      classNames={{ root: "admin-header-dropdown" }}
      getPopupContainer={getAdminHeaderPopupContainer}
    >
      <Button
        type="text"
        className="admin-header-icon-btn"
        aria-label={ariaLabel}
        icon={<BgColorsOutlined />}
        title={ariaLabel}
      />
    </Dropdown>
  );
}
