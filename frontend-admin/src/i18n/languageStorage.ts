import { DEFAULT_TEXT_LOCALE, isSupportedTextLocale, normalizeTextLocale, type TextLocale } from './config';

export const APP_LANGUAGE_STORAGE_KEY = 'app_language';
const LEGACY_LANGUAGE_STORAGE_KEY = 'regkasse.admin.textLocale';

export function getStoredLanguage(): TextLocale {
  if (typeof window === 'undefined') return DEFAULT_TEXT_LOCALE;
  try {
    const raw = window.localStorage.getItem(APP_LANGUAGE_STORAGE_KEY) ?? window.localStorage.getItem(LEGACY_LANGUAGE_STORAGE_KEY);
    if (raw) {
      const normalized = raw.toLowerCase().trim();
      return isSupportedTextLocale(normalized) ? normalized : DEFAULT_TEXT_LOCALE;
    }
  } catch {
    // localStorage can fail in restricted browser modes; fall back safely.
  }

  // First-launch fallback: infer from browser locale for better UX parity with frontend.
  return normalizeTextLocale(window.navigator?.language);
}

export function setStoredLanguage(locale: string): TextLocale {
  const safeLocale = normalizeTextLocale(locale);
  if (typeof window !== 'undefined') {
    try {
      window.localStorage.setItem(APP_LANGUAGE_STORAGE_KEY, safeLocale);
    } catch {
      // Keep runtime behavior stable even if persistence is unavailable.
    }
  }
  return safeLocale;
}
