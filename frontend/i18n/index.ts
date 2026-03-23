import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import * as Localization from 'expo-localization';
import { getSavedLanguage, saveLanguage } from './languageStorage';
import { DEFAULT_TEXT_LOCALE, normalizeTextLocale } from './localeUtils';
import 'intl-pluralrules';

// Import translation resources
import enAuth from './locales/en/auth.json';
import enCheckout from './locales/en/checkout.json';
import enCommon from './locales/en/common.json';
import enCustomers from './locales/en/customers.json';
import enEmployees from './locales/en/employees.json';
import enNavigation from './locales/en/navigation.json';
import enInvoices from './locales/en/invoices.json';
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
import deInvoices from './locales/de/invoices.json';
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
import trInvoices from './locales/tr/invoices.json';
import trOrders from './locales/tr/orders.json';
import trPayment from './locales/tr/payment.json';
import trProducts from './locales/tr/products.json';
import trReports from './locales/tr/reports.json';
import trSettings from './locales/tr/settings.json';
import trSystem from './locales/tr/system.json';
import trTables from './locales/tr/tables.json';

export const defaultNS = 'common';
const missingRuntimeKeys = new Set<string>();
const isDevRuntime =
  (typeof __DEV__ !== 'undefined' && Boolean(__DEV__)) ||
  process.env.NODE_ENV !== 'production';

function trackMissingRuntimeKey(lng: string, key: string) {
  const marker = `${lng}|${key}`;
  if (isDevRuntime || !missingRuntimeKeys.has(marker)) {
    console.warn(`[i18n-missing-key][frontend] lng="${lng}" key="${key}"`);
  }
  missingRuntimeKeys.add(marker);
}
/**
 * Runtime-registered POS namespaces (single source for drift checks).
 */
export const FRONTEND_REGISTERED_NAMESPACES = [
  'auth',
  'checkout',
  'common',
  'customers',
  'employees',
  'invoices',
  'navigation',
  'orders',
  'payment',
  'products',
  'reports',
  'settings',
  'system',
  'tables',
] as const;

export const resources = {
  en: {
    auth: enAuth,
    checkout: enCheckout,
    common: enCommon,
    customers: enCustomers,
    employees: enEmployees,
    invoices: enInvoices,
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
    invoices: deInvoices,
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
    invoices: trInvoices,
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

const SUPPORTED_LANGUAGES = ['en', 'de', 'tr'] as const;
const FALLBACK_LNG = DEFAULT_TEXT_LOCALE;

function normalizeLanguage(input: string | null | undefined): string {
  return normalizeTextLocale(input);
}

// Helper to switch language
export const changeLanguage = async (language: string) => {
  const normalized = normalizeLanguage(language);
  await saveLanguage(normalized);
  await i18n.changeLanguage(normalized);
};

const initI18n = async (): Promise<void> => {
  let languageToUse: string;
  try {
    const saved = await getSavedLanguage();
    const deviceLocaleTag = Localization.getLocales()[0]?.languageTag;
    languageToUse = normalizeLanguage(saved ?? deviceLocaleTag ?? FALLBACK_LNG);
  } catch {
    languageToUse = FALLBACK_LNG;
  }

  i18n.use(initReactI18next).init({
    resources,
    lng: languageToUse,
    fallbackLng: FALLBACK_LNG,
    supportedLngs: [...SUPPORTED_LANGUAGES],
    defaultNS,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
    compatibilityJSON: 'v3',
    parseMissingKeyHandler: (key) => {
      trackMissingRuntimeKey(i18n.language || FALLBACK_LNG, key);
      return key;
    },
  });
};

/** Uygulama kökünde (örn. _layout) await edilirse dil yüklenene kadar beklenir. */
export const i18nReady = initI18n();

export default i18n;

/** Text locale vs Intl formatting locale (re-export for consumers). */
export {
  DEFAULT_TEXT_LOCALE,
  DEFAULT_FORMAT_LOCALE,
  SUPPORTED_TEXT_LOCALES,
  TEXT_TO_FORMAT_LOCALE,
  normalizeTextLocale,
  normalizeFormatLocale,
  getFormattingLocaleForTextLocale,
} from './localeUtils';

export { formatDateTime, formatDate, formatTime, formatNumber } from './formatting';

/** Debug helper for runtime missing translations. */
export function getRuntimeMissingKeys() {
  return Array.from(missingRuntimeKeys).sort((a, b) => a.localeCompare(b));
}

/** Debug helper to reset in-memory missing translation tracker. */
export function clearRuntimeMissingKeys() {
  missingRuntimeKeys.clear();
}