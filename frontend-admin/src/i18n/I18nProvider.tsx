'use client';

import React, { createContext, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useState } from 'react';

const useIsoLayoutEffect = typeof window !== 'undefined' ? useLayoutEffect : useEffect;
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
import { getStoredLanguage, setStoredLanguage } from './languageStorage';
import { USER_FACING_MISSING_TRANSLATION_LABEL } from './translationFallback';
import { technicalConsole } from '@/shared/dev/technicalConsole';

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

const FORMAT_STORAGE_KEY = 'regkasse.admin.formatLocale';

const I18nContext = createContext<I18nContextValue | null>(null);

function parseTranslationKey(input: string): { namespace: string; path: string } {
  // Keep backward compatibility with existing "namespace.path" keys and
  // additionally accept "namespace:path" for cross-app usage discipline.
  if (input.includes(':')) {
    const [namespace, path] = input.split(':', 2);
    return { namespace, path };
  }

  const [namespace, ...rest] = input.split('.');
  return { namespace, path: rest.join('.') };
}

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
  if (isDevRuntime && !missingRuntimeKeys.has(marker)) {
    technicalConsole.warn(
      `[i18n] Missing translation key (dev diagnostic, English-only). locale="${locale}" key="${key}"`,
    );
  }
  missingRuntimeKeys.add(marker);
}

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [textLocale, setTextLocaleState] = useState<TextLocale>(DEFAULT_TEXT_LOCALE);
  const [formatLocale, setFormatLocaleState] = useState<string>(DEFAULT_FORMAT_LOCALE);
  const [isLocaleReady, setIsLocaleReady] = useState(false);

  useIsoLayoutEffect(() => {
    // Text locale: strict `de` when nothing stored (languageStorage); navigator is not used.
    // Layout effect on the client runs before paint so the first visible frame matches storage.
    const storedTextLocale = getStoredLanguage();
    const savedFormatLocale = typeof window !== 'undefined' ? window.localStorage.getItem(FORMAT_STORAGE_KEY) : null;
    const nextFormatLocale = savedFormatLocale
      ? normalizeFormatLocale(savedFormatLocale)
      : getFormattingLocaleForTextLocale(storedTextLocale);

    setTextLocaleState(storedTextLocale);
    setFormatLocaleState(nextFormatLocale);
    setIsLocaleReady(true);
  }, []);

  useEffect(() => {
    if (typeof document !== 'undefined') {
      document.documentElement.lang = textLocale;
    }
  }, [textLocale]);

  const setTextLocale = useCallback((next: string) => {
    const normalized = normalizeTextLocale(next);
    setTextLocaleState(normalized);
    if (typeof window !== 'undefined') {
      setStoredLanguage(normalized);
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
      const { namespace, path } = parseTranslationKey(key);
      const ns = namespace as AdminNamespace;
      const active = resolveFromPath((activeCatalog as Record<string, unknown>)[ns], path);
      const fallbackDe = resolveFromPath((fallbackCatalog as Record<string, unknown>)[ns], path);
      const resolved = active || fallbackDe;
      if (!resolved) {
        trackMissingRuntimeKey(textLocale, key);
        return USER_FACING_MISSING_TRANSLATION_LABEL;
      }
      return interpolate(resolved, options);
    };
    return { textLocale, formatLocale, setTextLocale, setFormatLocale, t };
  }, [textLocale, formatLocale, setTextLocale, setFormatLocale]);

  if (!isLocaleReady) {
    return null;
  }

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
