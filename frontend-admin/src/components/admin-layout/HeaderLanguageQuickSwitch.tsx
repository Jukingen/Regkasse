'use client';

/**
 * Top-bar UI language: `setTextLocale` → `setStoredLanguage` (`app_language`).
 * Option labels use fixed endonyms (Deutsch / English / Türkçe). Does not change formal report content resolution.
 */
import { useMemo } from 'react';
import { Button, Dropdown, Tooltip } from 'antd';
import type { MenuProps } from 'antd';
import { CheckOutlined, GlobalOutlined } from '@ant-design/icons';
import { useI18n, type TextLocale } from '@/i18n';
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
    [textLocale],
  );

  const handleMenuClick: MenuProps['onClick'] = ({ key }) => {
    setTextLocale(key as TextLocale);
  };

  const ariaLabel = t('adminShell.header.languageSelectAria');
  const tooltipHint = t('adminShell.header.languageSelectHint');

  return (
    <Tooltip title={tooltipHint} placement="bottomRight" mouseEnterDelay={0.35}>
      <fieldset style={{ border: 'none', margin: 0, padding: 0, minWidth: 0, display: 'inline-flex' }}>
        <legend className="admin-sr-only">{ariaLabel}</legend>
        <Dropdown
          menu={{
            items: menuItems,
            onClick: handleMenuClick,
            selectedKeys: [textLocale],
          }}
          trigger={['click']}
          placement="bottomRight"
          overlayClassName="language-switcher-dropdown admin-header-dropdown"
            getPopupContainer={getAdminHeaderPopupContainer}
        >
          <Button
            type="text"
            className="language-trigger"
            icon={<GlobalOutlined />}
            aria-label={ariaLabel}
            data-testid="admin-header-language-select"
          >
            <span className="language-code">{textLocale.toUpperCase()}</span>
          </Button>
        </Dropdown>
      </fieldset>
    </Tooltip>
  );
}
