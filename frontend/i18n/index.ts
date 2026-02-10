import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import * as Localization from 'expo-localization';
import { getSavedLanguage, saveLanguage } from './languageStorage';
import 'intl-pluralrules';

// Import translation resources
import enAuth from './locales/en/auth.json';
import enCheckout from './locales/en/checkout.json';
import enCommon from './locales/en/common.json';
import enCustomers from './locales/en/customers.json';
import enEmployees from './locales/en/employees.json';
import enNavigation from './locales/en/navigation.json';
import enOrders from './locales/en/orders.json';
import enPayment from './locales/en/payment.json';
import enProducts from './locales/en/products.json';
import enReports from './locales/en/reports.json';
import enSettings from './locales/en/settings.json';
import enSystem from './locales/en/system.json';
import enTables from './locales/en/tables.json';

import deAuth from './locales/de/auth.json';
import deCheckout from './locales/de/checkout.json';
import deCommon from './locales/de/common.json';
import deCustomers from './locales/de/customers.json';
import deEmployees from './locales/de/employees.json';
import deNavigation from './locales/de/navigation.json';
import deOrders from './locales/de/orders.json';
import dePayment from './locales/de/payment.json';
import deProducts from './locales/de/products.json';
import deReports from './locales/de/reports.json';
import deSettings from './locales/de/settings.json';
import deSystem from './locales/de/system.json';
import deTables from './locales/de/tables.json';

import trAuth from './locales/tr/auth.json';
import trCheckout from './locales/tr/checkout.json';
import trCommon from './locales/tr/common.json';
import trCustomers from './locales/tr/customers.json';
import trEmployees from './locales/tr/employees.json';
import trNavigation from './locales/tr/navigation.json';
import trOrders from './locales/tr/orders.json';
import trPayment from './locales/tr/payment.json';
import trProducts from './locales/tr/products.json';
import trReports from './locales/tr/reports.json';
import trSettings from './locales/tr/settings.json';
import trSystem from './locales/tr/system.json';
import trTables from './locales/tr/tables.json';

export const defaultNS = 'common';

export const resources = {
  en: {
    auth: enAuth,
    checkout: enCheckout,
    common: enCommon,
    customers: enCustomers,
    employees: enEmployees,
    navigation: enNavigation,
    orders: enOrders,
    payment: enPayment,
    products: enProducts,
    reports: enReports,
    settings: enSettings,
    system: enSystem,
    tables: enTables,
  },
  de: {
    auth: deAuth,
    checkout: deCheckout,
    common: deCommon,
    customers: deCustomers,
    employees: deEmployees,
    navigation: deNavigation,
    orders: deOrders,
    payment: dePayment,
    products: deProducts,
    reports: deReports,
    settings: deSettings,
    system: deSystem,
    tables: deTables,
  },
  tr: {
    auth: trAuth,
    checkout: trCheckout,
    common: trCommon,
    customers: trCustomers,
    employees: trEmployees,
    navigation: trNavigation,
    orders: trOrders,
    payment: trPayment,
    products: trProducts,
    reports: trReports,
    settings: trSettings,
    system: trSystem,
    tables: trTables,
  },
} as const;

// Helper to switch language
export const changeLanguage = async (language: string) => {
  await saveLanguage(language);
  await i18n.changeLanguage(language);
};

const initI18n = async () => {
  const savedLanguage = await getSavedLanguage();
  const languageToUse = savedLanguage || Localization.getLocales()[0]?.languageCode || 'en';

  i18n
    .use(initReactI18next)
    .init({
      resources,
      lng: languageToUse,
      fallbackLng: 'en',
      supportedLngs: ['en', 'de', 'tr'],
      defaultNS,
      interpolation: {
        escapeValue: false,
      },
      react: {
        useSuspense: false,
      },
      compatibilityJSON: 'v3',
    });
};

initI18n();

export default i18n;