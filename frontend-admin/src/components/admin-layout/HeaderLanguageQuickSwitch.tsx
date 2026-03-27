'use client';

/**
 * Top-bar UI language control: `setTextLocale` + `setStoredLanguage` (`app_language`).
 * Formal report content language (`ReportContentLanguage`: de | en) is resolved in
 * `reportContentLanguagePolicy` / `fiscalReportTextPolicy` — not controlled here.
 */
import { useMemo } from 'react';
import { Select, Space } from 'antd';
import { GlobalOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n';
import type { TextLocale } from '@/i18n';

export function HeaderLanguageQuickSwitch() {
  const { t, textLocale, setTextLocale } = useI18n();

  const options = useMemo(
    () =>
      [
        { value: 'de' as const, label: t('adminShell.header.languageNameDe') },
        { value: 'en' as const, label: t('adminShell.header.languageNameEn') },
        { value: 'tr' as const, label: t('adminShell.header.languageNameTr') },
      ] as const,
    [t],
  );

  const aria = t('adminShell.header.languageSelectAria');

  return (
    <Space align="center" size={6} wrap={false}>
      <GlobalOutlined aria-hidden style={{ color: 'var(--ant-color-text-tertiary)', fontSize: 14 }} />
      <Select<TextLocale>
        size="small"
        variant="filled"
        value={textLocale}
        onChange={(v) => setTextLocale(v)}
        options={[...options]}
        aria-label={aria}
        title={aria}
        popupMatchSelectWidth={false}
        showSearch={false}
        listHeight={256}
        styles={{ popup: { root: { minWidth: 160 } } }}
        style={{ minWidth: 118, maxWidth: 200 }}
      />
    </Space>
  );
}
