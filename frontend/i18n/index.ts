import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Localization from 'expo-localization';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

// Dil dosyalarını import et
import de from './locales/de.json';
import en from './locales/en.json';
import tr from './locales/tr.json';

// Dil kaynakları
const resources = {
  de: { translation: de },
  en: { translation: en },
  tr: { translation: tr }
};

// i18n yapılandırması
i18n
  .use(initReactI18next)
  .init({
    resources,
    lng: 'de', // Varsayılan dil Almanca
    fallbackLng: 'de',
    interpolation: {
      escapeValue: false,
    },
    react: {
      useSuspense: false,
    },
  });

// Dil tercihini kaydet
export const setLanguage = async (language: string) => {
  try {
    await AsyncStorage.setItem('userLanguage', language);
    await i18n.changeLanguage(language);
  } catch (error) {
    console.error('Dil değiştirilirken hata:', error);
  }
};

// Kaydedilmiş dil tercihini yükle
export const loadSavedLanguage = async () => {
  try {
    const savedLanguage = await AsyncStorage.getItem('userLanguage');
    if (savedLanguage) {
      await i18n.changeLanguage(savedLanguage);
    }
  } catch (error) {
    console.error('Kaydedilmiş dil yüklenirken hata:', error);
  }
};

// Uygulama başlarken kaydedilmiş dili yükle
loadSavedLanguage();

export default i18n; 