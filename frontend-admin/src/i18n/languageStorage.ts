import {
  DEFAULT_TEXT_LOCALE,
  type TextLocale,
  isSupportedTextLocale,
  normalizeTextLocale,
} from './config';

export const APP_LANGUAGE_STORAGE_KEY = 'app_language';
const LEGACY_LANGUAGE_STORAGE_KEY = 'regkasse.admin.textLocale';

/**
 * Kalıcı olarak kayıtlı metin dilini döndürür.
 * Kayıt yoksa veya geçersizse her zaman `de` (strict default).
 * Tarayıcı diline asla düşmez — ilk açılış deterministik kalır.
 */
export function getStoredLanguage(): TextLocale {
  if (typeof window === 'undefined') return DEFAULT_TEXT_LOCALE;
  try {
    const raw =
      window.localStorage.getItem(APP_LANGUAGE_STORAGE_KEY) ??
      window.localStorage.getItem(LEGACY_LANGUAGE_STORAGE_KEY);
    if (raw) {
      const normalized = raw.toLowerCase().trim();
      return isSupportedTextLocale(normalized) ? normalized : DEFAULT_TEXT_LOCALE;
    }
  } catch {
    // localStorage can fail in restricted browser modes; fall back safely.
  }

  return DEFAULT_TEXT_LOCALE;
}

/**
 * Kullanıcı seçimini kaydeder; `normalizeTextLocale` ile güvenli hale getirir.
 */
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

/**
 * Tarayıcı dilinden (navigator.language) desteklenen metin diline çevirir.
 * İlk yükleme veya `getStoredLanguage` ile kullanılmaz; yalnızca isteğe bağlı öneri UI için.
 * SSR veya navigator yoksa `undefined`.
 */
export function getNavigatorTextLocaleSuggestion(): TextLocale | undefined {
  if (typeof window === 'undefined') return undefined;
  const raw = window.navigator?.language;
  if (!raw) return undefined;
  return normalizeTextLocale(raw);
}
