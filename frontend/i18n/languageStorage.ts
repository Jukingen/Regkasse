import AsyncStorage from '@react-native-async-storage/async-storage';

const LANGUAGE_KEY = 'user-language';

/**
 * Web'de window/localStorage, SSR veya native ortamda tanımlı olmayabilir.
 * Sadece gerçekten mevcutsa localStorage kullan.
 */
function hasLocalStorage(): boolean {
  if (typeof globalThis === 'undefined') return false;
  try {
    const w = globalThis as unknown as { window?: Window; localStorage?: Storage };
    return typeof w.window !== 'undefined' && typeof w.localStorage !== 'undefined';
  } catch {
    return false;
  }
}

/**
 * Seçilen dili saklar.
 * Web'de localStorage, native/test/SSR'da AsyncStorage kullanır.
 */
export async function saveLanguage(language: string): Promise<void> {
  try {
    if (hasLocalStorage()) {
      (globalThis as unknown as { localStorage: Storage }).localStorage.setItem(LANGUAGE_KEY, language);
    } else {
      await AsyncStorage.setItem(LANGUAGE_KEY, language);
    }
  } catch (error) {
    console.error('Failed to save language:', error);
  }
}

/**
 * Saklanan dili okur.
 * Web'de localStorage, native/test/SSR'da AsyncStorage kullanır.
 * @returns Kayıtlı dil kodu veya yoksa null.
 */
export async function getSavedLanguage(): Promise<string | null> {
  try {
    if (hasLocalStorage()) {
      return (globalThis as unknown as { localStorage: Storage }).localStorage.getItem(LANGUAGE_KEY);
    }
    return await AsyncStorage.getItem(LANGUAGE_KEY);
  } catch (error) {
    console.error('Failed to get saved language:', error);
    return null;
  }
}
