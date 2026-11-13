import { normalizeTextLocale } from './localeUtils';
import { storage } from '../utils/storage';

const LANGUAGE_KEY = 'user-language';

/**
 * Persist selected UI language (non-sensitive preference).
 * Uses {@link storage} (localStorage on web, AsyncStorage on native).
 */
export async function saveLanguage(language: string): Promise<void> {
  const safeLanguage = normalizeTextLocale(language);
  try {
    await storage.setItem(LANGUAGE_KEY, safeLanguage);
  } catch (error) {
    console.error('Failed to save language:', error);
  }
}

/**
 * Read persisted UI language.
 */
export async function getSavedLanguage(): Promise<string | null> {
  try {
    return await storage.getItem(LANGUAGE_KEY);
  } catch (error) {
    console.error('Failed to get saved language:', error);
    return null;
  }
}
