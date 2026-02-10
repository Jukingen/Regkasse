import AsyncStorage from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';

const LANGUAGE_KEY = 'user-language';

/**
 * Persists the selected language to storage.
 * @param language The language code to save (e.g. 'en', 'de', 'tr')
 */
export const saveLanguage = async (language: string): Promise<void> => {
    try {
        if (Platform.OS === 'web') {
            localStorage.setItem(LANGUAGE_KEY, language);
        } else {
            await AsyncStorage.setItem(LANGUAGE_KEY, language);
        }
    } catch (error) {
        console.error('Failed to save language:', error);
    }
};

/**
 * Retrieves the persisted language from storage.
 * @returns The saved language code or null if not found.
 */
export const getSavedLanguage = async (): Promise<string | null> => {
    try {
        if (Platform.OS === 'web') {
            return localStorage.getItem(LANGUAGE_KEY);
        } else {
            return await AsyncStorage.getItem(LANGUAGE_KEY);
        }
    } catch (error) {
        console.error('Failed to get saved language:', error);
        return null;
    }
};
