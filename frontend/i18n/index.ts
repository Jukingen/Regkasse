import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Localization from 'expo-localization';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

// Dil dosyaları
const resources = {
  de: {
    translation: {
      // Genel
      appName: 'KasseAPP',
      loading: 'Laden...',
      error: 'Fehler',
      success: 'Erfolg',
      save: 'Speichern',
      cancel: 'Abbrechen',
      delete: 'Löschen',
      edit: 'Bearbeiten',
      search: 'Suchen...',

      // Auth
      login: 'Anmelden',
      logout: 'Abmelden',
      email: 'E-Mail',
      password: 'Passwort',
      forgotPassword: 'Passwort vergessen?',
      loginError: 'Anmeldung fehlgeschlagen',

      // Kasa
      cashRegister: 'Kasse',
      cart: 'Warenkorb',
      total: 'Gesamt',
      checkout: 'Bezahlen',
      addToCart: 'In den Warenkorb',
      removeFromCart: 'Aus Warenkorb entfernen',
      quantity: 'Menge',
      price: 'Preis',
      product: 'Produkt',
      products: 'Produkte',
      stock: 'Lager',
      outOfStock: 'Nicht verfügbar',

      // Ayarlar
      settings: 'Einstellungen',
      language: 'Sprache',
      theme: 'Design',
      notifications: 'Benachrichtigungen',
      darkMode: 'Dunkelmodus',
      lightMode: 'Hellmodus',
      systemTheme: 'System',
      german: 'Deutsch',
      english: 'Englisch',

      // Hata mesajları
      errorMessages: {
        required: '{{field}} ist erforderlich',
        invalidEmail: 'Ungültige E-Mail-Adresse',
        invalidPassword: 'Ungültiges Passwort',
        networkError: 'Netzwerkfehler',
        serverError: 'Serverfehler',
        sessionExpired: 'Sitzung abgelaufen',
      },

      // Bildirimler
      notificationSettings: {
        saleComplete: 'Verkauf abgeschlossen',
        lowStock: 'Niedriger Lagerbestand',
        dailyReport: 'Tagesabschluss',
      },
    },
  },
  en: {
    translation: {
      // General
      appName: 'KasseAPP',
      loading: 'Loading...',
      error: 'Error',
      success: 'Success',
      save: 'Save',
      cancel: 'Cancel',
      delete: 'Delete',
      edit: 'Edit',
      search: 'Search...',

      // Auth
      login: 'Login',
      logout: 'Logout',
      email: 'Email',
      password: 'Password',
      forgotPassword: 'Forgot Password?',
      loginError: 'Login failed',

      // Cash Register
      cashRegister: 'Cash Register',
      cart: 'Cart',
      total: 'Total',
      checkout: 'Checkout',
      addToCart: 'Add to Cart',
      removeFromCart: 'Remove from Cart',
      quantity: 'Quantity',
      price: 'Price',
      product: 'Product',
      products: 'Products',
      stock: 'Stock',
      outOfStock: 'Out of Stock',

      // Settings
      settings: 'Settings',
      language: 'Language',
      theme: 'Theme',
      notifications: 'Notifications',
      darkMode: 'Dark Mode',
      lightMode: 'Light Mode',
      systemTheme: 'System',
      german: 'German',
      english: 'English',

      // Error Messages
      errorMessages: {
        required: '{{field}} is required',
        invalidEmail: 'Invalid email address',
        invalidPassword: 'Invalid password',
        networkError: 'Network error',
        serverError: 'Server error',
        sessionExpired: 'Session expired',
      },

      // Notifications
      notificationSettings: {
        saleComplete: 'Sale completed',
        lowStock: 'Low stock alert',
        dailyReport: 'Daily report',
      },
    },
  },
};

// i18n yapılandırması
i18n
  .use(initReactI18next)
  .init({
    resources,
    lng: Localization.locale.split('-')[0], // Varsayılan dil
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

export default i18n; 