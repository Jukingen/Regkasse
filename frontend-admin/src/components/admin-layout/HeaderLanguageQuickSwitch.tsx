'use client';

/**
 * Top-bar UI language: `setTextLocale` → `setStoredLanguage` (`app_language`).
 * Option labels use fixed endonyms (Deutsch / English / Türkçe). Does not change formal report content resolution.
 */
import { useMemo } from 'react';
import { Select, Space, Tooltip } from 'antd';
import { GlobalOutlined } from '@ant-design/icons';
import { useI18n, SUPPORTED_TEXT_LOCALES, type TextLocale } from '@/i18n';

/** Canonical names for the switcher (stable regardless of current UI locale). */
const UI_LANGUAGE_ENDONYMS: Record<TextLocale, string> = {
  de: 'Deutsch',
  en: 'English',
  tr: 'Türkçe',
};

export function HeaderLanguageQuickSwitch() {
  const { t, textLocale, setTextLocale } = useI18n();

  const options = useMemo(
    () =>
      SUPPORTED_TEXT_LOCALES.map((value) => ({
        value,
        label: UI_LANGUAGE_ENDONYMS[value],
      })),
    [],
  );

  const ariaLabel = t('adminShell.header.languageSelectAria');
  const tooltipTitle = useMemo(
    () => (
      <div>
        <div>{t('adminShell.header.languageSelectTitle')}</div>
        <div style={{ marginTop: 6, fontSize: 12, opacity: 0.92, fontWeight: 400 }}>
          {t('adminShell.header.languageSelectHint')}
        </div>
      </div>
    ),
    [t],
  );

  return (
    <Tooltip title={tooltipTitle} placement="bottomRight" mouseEnterDelay={0.35}>
      <fieldset style={{ border: 'none', margin: 0, padding: 0, minWidth: 0, display: 'inline-flex' }}>
        <legend className="admin-sr-only">{t('adminShell.header.languageSelectTitle')}</legend>
        <Space align="center" size={8} wrap={false} style={{ flexShrink: 0 }}>
          <GlobalOutlined aria-hidden style={{ color: 'var(--ant-color-text-tertiary)', fontSize: 16 }} />
          <Select<TextLocale>
            id="admin-header-ui-language"
            size="middle"
            variant="filled"
            value={textLocale}
            onChange={(v) => setTextLocale(v)}
            options={options}
            aria-label={ariaLabel}
            popupMatchSelectWidth={false}
            showSearch={false}
            listHeight={256}
            styles={{ popup: { root: { minWidth: 160 } } }}
            style={{ minWidth: 148, maxWidth: 240 }}
            classNames={{ root: 'admin-header-language-select' }}
            getPopupContainer={(trigger) => trigger.parentElement ?? document.body}
            data-testid="admin-header-language-select"
          />
        </Space>
      </fieldset>
    </Tooltip>
  );
}
