'use client';

/**
 * Top-bar UI language: `setTextLocale` → `setStoredLanguage` (`app_language`).
 * Shows only the active locale code (de / en / tr) — no visible title or globe icon.
 */
import { CheckOutlined } from '@ant-design/icons';
import type { MenuProps } from 'antd';
import { Button, Dropdown } from 'antd';
import { useMemo } from 'react';

import { type TextLocale, useI18n } from '@/i18n';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';

const UI_LANGUAGE_OPTIONS: ReadonlyArray<{ code: TextLocale; name: string; flag: string }> = [
  { code: 'de', name: 'Deutsch', flag: '🇩🇪' },
  { code: 'en', name: 'English', flag: '🇬🇧' },
  { code: 'tr', name: 'Türkçe', flag: '🇹🇷' },
];

export function HeaderLanguageQuickSwitch() {
  const { t, textLocale, setTextLocale } = useI18n();

  const menuItems: MenuProps['items'] = useMemo(
    () =>
      UI_LANGUAGE_OPTIONS.map((locale) => ({
        key: locale.code,
        label: (
          <span className="language-menu-item">
            <span className="language-flag" aria-hidden>
              {locale.flag}
            </span>
            <span className="language-name">{locale.name}</span>
            {locale.code === textLocale ? (
              <CheckOutlined className="language-check" aria-hidden />
            ) : null}
          </span>
        ),
      })),
    [textLocale]
  );

  const handleMenuClick: MenuProps['onClick'] = ({ key }) => {
    setTextLocale(key as TextLocale);
  };

  return (
    <Dropdown
      menu={{
        items: menuItems,
        onClick: handleMenuClick,
        selectedKeys: [textLocale],
      }}
      trigger={['click']}
      placement="bottomRight"
      classNames={{ root: 'language-switcher-dropdown admin-header-dropdown' }}
      getPopupContainer={getAdminHeaderPopupContainer}
    >
      <Button
        type="default"
        size="small"
        className="admin-header-tool-btn language-trigger"
        aria-label={t('adminShell.header.languageSelectAria')}
        data-testid="admin-header-language-select"
      >
        <span className="language-code">{textLocale}</span>
      </Button>
    </Dropdown>
  );
}
