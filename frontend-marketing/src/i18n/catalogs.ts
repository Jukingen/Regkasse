import de from './de.json';
import en from './en.json';
import tr from './tr.json';
import type { Catalog, Lang } from './types';

export const catalogs: Record<Lang, Catalog> = {
  de: de as Catalog,
  en: en as Catalog,
  tr: tr as Catalog,
};

export const MARKETING_LANG_KEY = 'marketing_lang';

export function isLang(value: string | null): value is Lang {
  return value === 'de' || value === 'en' || value === 'tr';
}

export function readMarketingLang(): Lang {
  if (typeof window === 'undefined') return 'de';
  try {
    const stored = window.localStorage.getItem(MARKETING_LANG_KEY);
    return isLang(stored) ? stored : 'de';
  } catch {
    return 'de';
  }
}

export function writeMarketingLang(lang: Lang): void {
  window.localStorage.setItem(MARKETING_LANG_KEY, lang);
  document.documentElement.lang = lang;
}
