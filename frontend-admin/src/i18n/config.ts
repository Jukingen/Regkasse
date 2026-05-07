import deAdminShell from './locales/de/admin-shell.json';
import deCommon from './locales/de/common.json';
import deNav from './locales/de/nav.json';
import deUsers from './locales/de/users.json';
import deSettings from './locales/de/settings.json';
import deProducts from './locales/de/products.json';
import deFinanzOnlineOutbox from './locales/de/finanzOnlineOutbox.json';
import deFinanzOnlineReconciliation from './locales/de/finanzOnlineReconciliation.json';
import deRksvHub from './locales/de/rksvHub.json';
import deReceipts from './locales/de/receipts.json';
import deReceiptTemplates from './locales/de/receiptTemplates.json';
import dePayments from './locales/de/payments.json';
import deTagesabschluss from './locales/de/tagesabschluss.json';
import deReporting from './locales/de/reporting.json';
import deErrors from './locales/de/errors.json';
import deModifierGroups from './locales/de/modifierGroups.json';
import deBenefits from './locales/de/benefits.json';
import deCustomers from './locales/de/customers.json';
import deInvoices from './locales/de/invoices.json';
import deBackupDr from './locales/de/backupDr.json';
import deTimeSync from './locales/de/timeSync.json';
import deVouchers from './locales/de/vouchers.json';
import deFiscalExportAudit from './locales/de/fiscalExportAudit.json';
import enAdminShell from './locales/en/admin-shell.json';
import enCommon from './locales/en/common.json';
import enNav from './locales/en/nav.json';
import enUsers from './locales/en/users.json';
import enSettings from './locales/en/settings.json';
import enProducts from './locales/en/products.json';
import enFinanzOnlineOutbox from './locales/en/finanzOnlineOutbox.json';
import enFinanzOnlineReconciliation from './locales/en/finanzOnlineReconciliation.json';
import enRksvHub from './locales/en/rksvHub.json';
import enReceipts from './locales/en/receipts.json';
import enReceiptTemplates from './locales/en/receiptTemplates.json';
import enPayments from './locales/en/payments.json';
import enTagesabschluss from './locales/en/tagesabschluss.json';
import enReporting from './locales/en/reporting.json';
import enErrors from './locales/en/errors.json';
import enModifierGroups from './locales/en/modifierGroups.json';
import enBenefits from './locales/en/benefits.json';
import enCustomers from './locales/en/customers.json';
import enInvoices from './locales/en/invoices.json';
import enBackupDr from './locales/en/backupDr.json';
import enTimeSync from './locales/en/timeSync.json';
import enVouchers from './locales/en/vouchers.json';
import enFiscalExportAudit from './locales/en/fiscalExportAudit.json';
import trAdminShell from './locales/tr/admin-shell.json';
import trCommon from './locales/tr/common.json';
import trNav from './locales/tr/nav.json';
import trUsers from './locales/tr/users.json';
import trSettings from './locales/tr/settings.json';
import trProducts from './locales/tr/products.json';
import trFinanzOnlineOutbox from './locales/tr/finanzOnlineOutbox.json';
import trFinanzOnlineReconciliation from './locales/tr/finanzOnlineReconciliation.json';
import trRksvHub from './locales/tr/rksvHub.json';
import trReceipts from './locales/tr/receipts.json';
import trReceiptTemplates from './locales/tr/receiptTemplates.json';
import trPayments from './locales/tr/payments.json';
import trTagesabschluss from './locales/tr/tagesabschluss.json';
import trReporting from './locales/tr/reporting.json';
import trErrors from './locales/tr/errors.json';
import trModifierGroups from './locales/tr/modifierGroups.json';
import trBenefits from './locales/tr/benefits.json';
import trCustomers from './locales/tr/customers.json';
import trInvoices from './locales/tr/invoices.json';
import trBackupDr from './locales/tr/backupDr.json';
import trTimeSync from './locales/tr/timeSync.json';
import trVouchers from './locales/tr/vouchers.json';
import trFiscalExportAudit from './locales/tr/fiscalExportAudit.json';

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
    receipts: deReceipts,
    receiptTemplates: deReceiptTemplates,
    payments: dePayments,
    tagesabschluss: deTagesabschluss,
    reporting: deReporting,
    errors: deErrors,
    modifierGroups: deModifierGroups,
    benefits: deBenefits,
    customers: deCustomers,
    invoices: deInvoices,
    backupDr: deBackupDr,
    timeSync: deTimeSync,
    vouchers: deVouchers,
    fiscalExportAudit: deFiscalExportAudit,
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
    receipts: enReceipts,
    receiptTemplates: enReceiptTemplates,
    payments: enPayments,
    tagesabschluss: enTagesabschluss,
    reporting: enReporting,
    errors: enErrors,
    modifierGroups: enModifierGroups,
    benefits: enBenefits,
    customers: enCustomers,
    invoices: enInvoices,
    backupDr: enBackupDr,
    timeSync: enTimeSync,
    vouchers: enVouchers,
    fiscalExportAudit: enFiscalExportAudit,
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
    receipts: trReceipts,
    receiptTemplates: trReceiptTemplates,
    payments: trPayments,
    tagesabschluss: trTagesabschluss,
    reporting: trReporting,
    errors: trErrors,
    modifierGroups: trModifierGroups,
    benefits: trBenefits,
    customers: trCustomers,
    invoices: trInvoices,
    backupDr: trBackupDr,
    timeSync: trTimeSync,
    vouchers: trVouchers,
    fiscalExportAudit: trFiscalExportAudit,
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
