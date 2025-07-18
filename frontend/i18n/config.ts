import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Localization from 'expo-localization';
import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

// Dil dosyalarını import et
import de from './locales/de.json';
import en from './locales/en.json';
import tr from './locales/tr.json';

const resources = {
  'de-DE': { translation: de },
  'en': { translation: en },
  'tr': { translation: tr },
};

const getInitialLanguage = () => {
  // AsyncStorage veya localStorage'dan dil alınır, yoksa de-DE döner
  if (typeof window !== 'undefined' && window.localStorage) {
    return window.localStorage.getItem('language') || 'de-DE';
  }
  return 'de-DE';
};

i18n
  .use(initReactI18next)
  .init({
    resources,
    lng: getInitialLanguage(),
    fallbackLng: 'de-DE',
    interpolation: {
      escapeValue: false,
    },
    compatibilityJSON: 'v3',
  });

export default i18n; 