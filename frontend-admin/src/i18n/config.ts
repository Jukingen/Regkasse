import deAdminShell from './locales/de/admin-shell.json';
import deCommon from './locales/de/common.json';
import deNav from './locales/de/nav.json';
import deUsers from './locales/de/users.json';
import deSettings from './locales/de/settings.json';
import deProducts from './locales/de/products.json';
import deFinanzOnlineOutbox from './locales/de/finanzOnlineOutbox.json';
import deFinanzOnlineReconciliation from './locales/de/finanzOnlineReconciliation.json';
import deRksvHub from './locales/de/rksvHub.json';
import enAdminShell from './locales/en/admin-shell.json';
import enCommon from './locales/en/common.json';
import enNav from './locales/en/nav.json';
import enUsers from './locales/en/users.json';
import enSettings from './locales/en/settings.json';
import enProducts from './locales/en/products.json';
import enFinanzOnlineOutbox from './locales/en/finanzOnlineOutbox.json';
import enFinanzOnlineReconciliation from './locales/en/finanzOnlineReconciliation.json';
import enRksvHub from './locales/en/rksvHub.json';
import trAdminShell from './locales/tr/admin-shell.json';
import trCommon from './locales/tr/common.json';
import trNav from './locales/tr/nav.json';
import trUsers from './locales/tr/users.json';
import trSettings from './locales/tr/settings.json';
import trProducts from './locales/tr/products.json';
import trFinanzOnlineOutbox from './locales/tr/finanzOnlineOutbox.json';
import trFinanzOnlineReconciliation from './locales/tr/finanzOnlineReconciliation.json';
import trRksvHub from './locales/tr/rksvHub.json';

export const SUPPORTED_TEXT_LOCALES = ['de', 'en', 'tr'] as const;
export type TextLocale = (typeof SUPPORTED_TEXT_LOCALES)[number];

export const DEFAULT_TEXT_LOCALE: TextLocale = 'de';
export const TEXT_TO_FORMAT_LOCALE: Record<TextLocale, string> = {
  de: 'de-AT',
  en: 'en-US',
  tr: 'tr-TR',
};
export const DEFAULT_FORMAT_LOCALE = TEXT_TO_FORMAT_LOCALE[DEFAULT_TEXT_LOCALE];

const catalogs = {
  de: {
    adminShell: deAdminShell,
    common: deCommon,
    nav: deNav,
    users: deUsers,
    settings: deSettings,
    products: deProducts,
    finanzOnlineOutbox: deFinanzOnlineOutbox,
    finanzOnlineReconciliation: deFinanzOnlineReconciliation,
    rksvHub: deRksvHub,
  },
  en: {
    adminShell: enAdminShell,
    common: enCommon,
    nav: enNav,
    users: enUsers,
    settings: enSettings,
    products: enProducts,
    finanzOnlineOutbox: enFinanzOnlineOutbox,
    finanzOnlineReconciliation: enFinanzOnlineReconciliation,
    rksvHub: enRksvHub,
  },
  tr: {
    adminShell: trAdminShell,
    common: trCommon,
    nav: trNav,
    users: trUsers,
    settings: trSettings,
    products: trProducts,
    finanzOnlineOutbox: trFinanzOnlineOutbox,
    finanzOnlineReconciliation: trFinanzOnlineReconciliation,
    rksvHub: trRksvHub,
  },
} as const;

export type AdminNamespace = keyof (typeof catalogs)['de'];
export const ADMIN_REGISTERED_NAMESPACES = Object.freeze(
  Object.keys(catalogs.de) as AdminNamespace[],
);

export function isSupportedTextLocale(value: string): value is TextLocale {
  return (SUPPORTED_TEXT_LOCALES as readonly string[]).includes(value);
}

export function normalizeTextLocale(input: string | null | undefined): TextLocale {
  if (!input) return DEFAULT_TEXT_LOCALE;
  const normalized = input.toLowerCase().replace('_', '-');
  if (isSupportedTextLocale(normalized)) return normalized;
  if (normalized.startsWith('de')) return 'de';
  if (normalized.startsWith('en')) return 'en';
  if (normalized.startsWith('tr')) return 'tr';
  return DEFAULT_TEXT_LOCALE;
}

export function normalizeFormatLocale(input: string | null | undefined): string {
  if (!input) return DEFAULT_FORMAT_LOCALE;
  const normalized = input.toLowerCase().replace('_', '-');
  if (normalized === 'de' || normalized.startsWith('de-')) return 'de-AT';
  if (normalized === 'en' || normalized.startsWith('en-')) return 'en-US';
  if (normalized === 'tr' || normalized.startsWith('tr-')) return 'tr-TR';
  return DEFAULT_FORMAT_LOCALE;
}

export function getFormattingLocaleForTextLocale(input: string | null | undefined): string {
  return TEXT_TO_FORMAT_LOCALE[normalizeTextLocale(input)];
}

export function getCatalog(locale: TextLocale) {
  return catalogs[locale];
}
