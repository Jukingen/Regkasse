'use client';

/**
 * UI language selector: `setTextLocale` → `setStoredLanguage` (`app_language`).
 * Axios sends `Accept-Language` on every request so backend API errors match the selected locale.
 * Option labels use fixed endonyms (Deutsch / English / Türkçe).
 */
import { Select } from 'antd';
import type { CSSProperties } from 'react';
import { useI18n, type TextLocale } from '@/i18n';

const LANGUAGE_OPTIONS: ReadonlyArray<{ value: TextLocale; label: string }> = [
    { value: 'de', label: 'Deutsch' },
    { value: 'en', label: 'English' },
    { value: 'tr', label: 'Türkçe' },
];

export type LanguageSwitcherProps = {
    className?: string;
    style?: CSSProperties;
    'data-testid'?: string;
};

export function LanguageSwitcher({ className, style, 'data-testid': dataTestId }: LanguageSwitcherProps) {
    const { textLocale, setTextLocale, t } = useI18n();

    return (
        <Select<TextLocale>
            className={className}
            style={{ width: 140, ...style }}
            value={textLocale}
            onChange={(lang) => setTextLocale(lang)}
            options={[...LANGUAGE_OPTIONS]}
            aria-label={t('adminShell.header.languageSelectAria')}
            data-testid={dataTestId ?? 'language-switcher'}
        />
    );
}
