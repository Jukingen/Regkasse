import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Localization from 'expo-localization';
import LanguageDetector from 'i18next-browser-languagedetector';

// Dil dosyalarını import et
import de from './locales/de.json';
import tr from './locales/tr.json';
import en from './locales/en.json';

// Özel dil algılama modülü
const customLanguageDetector = {
    name: 'customLanguageDetector',
    async: true,
    detect: async (callback: (lng: string) => void) => {
        try {
            const savedLanguage = await AsyncStorage.getItem('user-language');
            if (savedLanguage) {
                callback(savedLanguage);
                return;
            }
            
            // Varsayılan olarak Almanca kullan
            callback('de');
        } catch (error) {
            // Hata durumunda Almanca kullan
            callback('de');
        }
    },
    init: () => {},
    cacheUserLanguage: async (lng: string) => {
        try {
            await AsyncStorage.setItem('user-language', lng);
        } catch (error) {
            console.error('Error saving language:', error);
        }
    }
};

i18n
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
        resources: {
            de: { translation: de },
            tr: { translation: tr },
            en: { translation: en }
        },
        lng: 'de', // Varsayılan dil Almanca
        fallbackLng: 'de', // Fallback de Almanca
        defaultNS: 'translation',
        interpolation: {
            escapeValue: false
        },
        react: {
            useSuspense: false
        },
        detection: {
            order: ['customLanguageDetector', 'navigator'],
            lookupFromPathIndex: 0
        }
    });

// Özel dil algılayıcıyı ekle
i18n.services.languageDetector.addDetector(customLanguageDetector);

export const changeLanguage = async (language: 'de' | 'tr' | 'en') => {
    try {
        await AsyncStorage.setItem('user-language', language);
        await i18n.changeLanguage(language);
    } catch (error) {
        console.error('Error changing language:', error);
    }
};

export default i18n; 