'use client';

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import {
  DEFAULT_FORMAT_LOCALE,
  DEFAULT_TEXT_LOCALE,
  getCatalog,
  getFormattingLocaleForTextLocale,
  normalizeFormatLocale,
  normalizeTextLocale,
  type AdminNamespace,
  type TextLocale,
} from './config';

type TranslateOptions = Record<string, string | number>;
const missingRuntimeKeys = new Set<string>();
const isDevRuntime = process.env.NODE_ENV !== 'production';

type I18nContextValue = {
  textLocale: TextLocale;
  formatLocale: string;
  setTextLocale: (next: string) => void;
  setFormatLocale: (next: string) => void;
  t: (key: string, options?: TranslateOptions) => string;
};

const I18N_STORAGE_KEY = 'regkasse.admin.textLocale';
const FORMAT_STORAGE_KEY = 'regkasse.admin.formatLocale';

const I18nContext = createContext<I18nContextValue | null>(null);

function resolveFromPath(source: unknown, key: string): string | undefined {
  const value = key.split('.').reduce<unknown>((acc, segment) => {
    if (!acc || typeof acc !== 'object') return undefined;
    return (acc as Record<string, unknown>)[segment];
  }, source);
  return typeof value === 'string' ? value : undefined;
}

function interpolate(template: string, options?: TranslateOptions): string {
  if (!options) return template;
  return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, (_, token: string) =>
    options[token] !== undefined ? String(options[token]) : `{{${token}}}`,
  );
}

function trackMissingRuntimeKey(locale: string, key: string) {
  const marker = `${locale}|${key}`;
  if (isDevRuntime || !missingRuntimeKeys.has(marker)) {
    console.warn(`[i18n-missing-key][frontend-admin] locale="${locale}" key="${key}"`);
  }
  missingRuntimeKeys.add(marker);
}

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const initialTextLocale = (() => {
    if (typeof window === 'undefined') return DEFAULT_TEXT_LOCALE;
    const saved = window.localStorage.getItem(I18N_STORAGE_KEY);
    return normalizeTextLocale(saved ?? window.navigator.language);
  })();
  const initialFormatLocale = (() => {
    if (typeof window === 'undefined') return DEFAULT_FORMAT_LOCALE;
    const saved = window.localStorage.getItem(FORMAT_STORAGE_KEY);
    return saved ? normalizeFormatLocale(saved) : getFormattingLocaleForTextLocale(initialTextLocale);
  })();

  const [textLocale, setTextLocaleState] = useState<TextLocale>(initialTextLocale);
  const [formatLocale, setFormatLocaleState] = useState<string>(initialFormatLocale);

  useEffect(() => {
    if (typeof document !== 'undefined') {
      document.documentElement.lang = textLocale;
    }
  }, [textLocale]);

  const setTextLocale = useCallback((next: string) => {
    const normalized = normalizeTextLocale(next);
    setTextLocaleState(normalized);
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(I18N_STORAGE_KEY, normalized);
      document.documentElement.lang = normalized;
    }
  }, []);

  const setFormatLocale = useCallback((next: string) => {
    const normalized = normalizeFormatLocale(next);
    setFormatLocaleState(normalized);
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(FORMAT_STORAGE_KEY, normalized);
    }
  }, []);

  const value = useMemo<I18nContextValue>(() => {
    const activeCatalog = getCatalog(textLocale);
    const fallbackCatalog = getCatalog(DEFAULT_TEXT_LOCALE);
    const t = (key: string, options?: TranslateOptions): string => {
      const [namespace, ...rest] = key.split('.');
      const path = rest.join('.');
      const ns = namespace as AdminNamespace;
      const active = resolveFromPath((activeCatalog as Record<string, unknown>)[ns], path);
      const fallback = resolveFromPath((fallbackCatalog as Record<string, unknown>)[ns], path);
      const resolved = active || fallback;
      if (!resolved) {
        trackMissingRuntimeKey(textLocale, key);
        return key;
      }
      return interpolate(resolved, options);
    };
    return { textLocale, formatLocale, setTextLocale, setFormatLocale, t };
  }, [textLocale, formatLocale, setTextLocale, setFormatLocale]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) {
    throw new Error('useI18n must be used inside I18nProvider');
  }
  return ctx;
}

/** Debug helper for runtime missing translations. */
export function getRuntimeMissingKeys() {
  return Array.from(missingRuntimeKeys).sort((a, b) => a.localeCompare(b));
}

/** Debug helper to reset in-memory missing translation tracker. */
export function clearRuntimeMissingKeys() {
  missingRuntimeKeys.clear();
}
