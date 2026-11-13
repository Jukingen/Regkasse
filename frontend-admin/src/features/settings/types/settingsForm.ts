/**
 * Type-safe mapping between CompanySettings API response and UpdateCompanySettingsRequest.
 * Duplicate form field names are avoided (e.g. finanzOnlineParticipantId vs finanzOnlineUsername).
 */
import type { CompanySettings, UpdateCompanySettingsRequest } from '@/api/generated/model';

export type SettingsFormValues = Omit<UpdateCompanySettingsRequest, 'businessHours'> & {
  businessHours?: Record<string, string>;
  finanzOnlineEnabled?: boolean;
  finanzOnlineParticipantId?: string;
  finanzOnlinePin?: string;
  finanzOnlineApiUrl?: string;
  finanzOnlineSubmitInterval?: number;
  finanzOnlineRetryAttempts?: number;
  finanzOnlineAutoSubmit?: boolean;
  finanzOnlineEnableValidation?: boolean;
  lastFinanzOnlineSync?: string;
  pendingInvoices?: number;
  defaultTseDeviceId?: string;
  tseAutoConnect?: boolean;
  tseConnectionTimeout?: number;
};

/** Fallback when tenant settings row is missing required PUT fields. */
export const SETTINGS_SHELL_DEFAULTS: Pick<
  SettingsFormValues,
  | 'defaultCurrency'
  | 'defaultLanguage'
  | 'defaultTimeZone'
  | 'defaultDateFormat'
  | 'defaultTimeFormat'
  | 'defaultDecimalPlaces'
  | 'defaultPaymentMethod'
  | 'taxCalculationMethod'
  | 'invoiceNumbering'
  | 'receiptNumbering'
> = {
  defaultCurrency: 'EUR',
  defaultLanguage: 'de-DE',
  defaultTimeZone: 'Europe/Vienna',
  defaultDateFormat: 'dd.MM.yyyy',
  defaultTimeFormat: 'HH:mm:ss',
  defaultDecimalPlaces: 2,
  defaultPaymentMethod: 'Cash',
  taxCalculationMethod: 'Standard',
  invoiceNumbering: 'Sequential',
  receiptNumbering: 'Sequential',
};

/** CompanySettings -> form values (API response uses different keys for some fields) */
export function mapSettingsToFormValues(
  s: CompanySettings | undefined
): Partial<SettingsFormValues> {
  if (!s) return {};
  return {
    companyName: s.companyName,
    companyAddress: s.companyAddress,
    companyPhone: s.companyPhone,
    companyEmail: s.companyEmail,
    companyWebsite: s.companyWebsite,
    companyTaxNumber: s.companyTaxNumber,
    companyRegistrationNumber: s.companyRegistrationNumber,
    companyVatNumber: s.companyVatNumber,
    companyLogo: s.companyLogo,
    companyDescription: s.companyDescription,
    contactPerson: s.contactPerson,
    contactPhone: s.contactPhone,
    contactEmail: s.contactEmail,
    bankName: s.bankName,
    bankAccountNumber: s.bankAccountNumber,
    bankRoutingNumber: s.bankRoutingNumber,
    bankSwiftCode: s.bankSwiftCode,
    paymentTerms: s.paymentTerms,
    defaultCurrency: s.currency,
    defaultLanguage: s.language,
    defaultTimeZone: s.timeZone,
    defaultDateFormat: s.dateFormat,
    defaultTimeFormat: s.timeFormat,
    defaultDecimalPlaces: s.decimalPlaces,
    defaultPaymentMethod: s.defaultPaymentMethod,
    taxCalculationMethod: s.taxCalculationMethod,
    invoiceNumbering: s.invoiceNumbering,
    receiptNumbering: s.receiptNumbering,
    businessHours: s.businessHours as Record<string, string>,
    finanzOnlineEnabled: s.finanzOnlineEnabled,
    finanzOnlineApiUrl: s.finanzOnlineApiUrl ?? undefined,
    finanzOnlineParticipantId: s.finanzOnlineUsername ?? undefined,
    // Do not prefill secrets in settings form.
    finanzOnlinePin: undefined,
    finanzOnlineSubmitInterval: s.finanzOnlineSubmitInterval,
    finanzOnlineRetryAttempts: s.finanzOnlineRetryAttempts,
    finanzOnlineAutoSubmit: s.finanzOnlineAutoSubmit,
    finanzOnlineEnableValidation: s.finanzOnlineEnableValidation,
    lastFinanzOnlineSync: s.lastFinanzOnlineSync ?? undefined,
    pendingInvoices: s.pendingInvoices ?? undefined,
    defaultTseDeviceId: s.defaultTseDeviceId ?? undefined,
    tseAutoConnect: s.tseAutoConnect,
    tseConnectionTimeout: s.tseConnectionTimeout,
  };
}

/**
 * Merge partial form values (e.g. only the active Ant Design Tabs pane) with
 * existing tenant settings so PUT /api/company/settings always includes required fields.
 */
export function mergeSettingsFormForUpdate(
  partial: Partial<SettingsFormValues>,
  existing: CompanySettings | undefined | null
): SettingsFormValues {
  const base = mapSettingsToFormValues(existing ?? undefined);
  return {
    ...SETTINGS_SHELL_DEFAULTS,
    ...base,
    ...partial,
    companyName: partial.companyName ?? base.companyName ?? '',
    companyAddress: partial.companyAddress ?? base.companyAddress ?? '',
    companyTaxNumber: partial.companyTaxNumber ?? base.companyTaxNumber ?? '',
    defaultCurrency:
      partial.defaultCurrency ?? base.defaultCurrency ?? SETTINGS_SHELL_DEFAULTS.defaultCurrency,
    defaultLanguage:
      partial.defaultLanguage ?? base.defaultLanguage ?? SETTINGS_SHELL_DEFAULTS.defaultLanguage,
    defaultTimeZone:
      partial.defaultTimeZone ?? base.defaultTimeZone ?? SETTINGS_SHELL_DEFAULTS.defaultTimeZone,
    defaultDateFormat:
      partial.defaultDateFormat ??
      base.defaultDateFormat ??
      SETTINGS_SHELL_DEFAULTS.defaultDateFormat,
    defaultTimeFormat:
      partial.defaultTimeFormat ??
      base.defaultTimeFormat ??
      SETTINGS_SHELL_DEFAULTS.defaultTimeFormat,
    defaultDecimalPlaces:
      partial.defaultDecimalPlaces ??
      base.defaultDecimalPlaces ??
      SETTINGS_SHELL_DEFAULTS.defaultDecimalPlaces,
    defaultPaymentMethod:
      partial.defaultPaymentMethod ??
      base.defaultPaymentMethod ??
      SETTINGS_SHELL_DEFAULTS.defaultPaymentMethod,
    taxCalculationMethod:
      partial.taxCalculationMethod ??
      base.taxCalculationMethod ??
      SETTINGS_SHELL_DEFAULTS.taxCalculationMethod,
    invoiceNumbering:
      partial.invoiceNumbering ??
      base.invoiceNumbering ??
      SETTINGS_SHELL_DEFAULTS.invoiceNumbering,
    receiptNumbering:
      partial.receiptNumbering ??
      base.receiptNumbering ??
      SETTINGS_SHELL_DEFAULTS.receiptNumbering,
    businessHours: partial.businessHours ?? base.businessHours ?? {},
  };
}

/** Form values -> UpdateCompanySettingsRequest (includes FinanzOnline and TSE when backend supports) */
export function mapFormValuesToUpdateRequest(v: SettingsFormValues): UpdateCompanySettingsRequest {
  const base: UpdateCompanySettingsRequest = {
    companyName: v.companyName!,
    companyAddress: v.companyAddress!,
    companyPhone: v.companyPhone,
    companyEmail: v.companyEmail,
    companyWebsite: v.companyWebsite,
    companyTaxNumber: v.companyTaxNumber!,
    companyRegistrationNumber: v.companyRegistrationNumber,
    companyVatNumber: v.companyVatNumber,
    companyLogo: v.companyLogo,
    companyDescription: v.companyDescription,
    contactPerson: v.contactPerson,
    contactPhone: v.contactPhone,
    contactEmail: v.contactEmail,
    bankName: v.bankName,
    bankAccountNumber: v.bankAccountNumber,
    bankRoutingNumber: v.bankRoutingNumber,
    bankSwiftCode: v.bankSwiftCode,
    paymentTerms: v.paymentTerms,
    defaultCurrency: v.defaultCurrency!,
    defaultLanguage: v.defaultLanguage!,
    defaultTimeZone: v.defaultTimeZone!,
    defaultDateFormat: v.defaultDateFormat!,
    defaultTimeFormat: v.defaultTimeFormat!,
    defaultDecimalPlaces: v.defaultDecimalPlaces ?? 2,
    defaultPaymentMethod: v.defaultPaymentMethod!,
    taxCalculationMethod: v.taxCalculationMethod!,
    invoiceNumbering: v.invoiceNumbering!,
    receiptNumbering: v.receiptNumbering!,
    businessHours: (v.businessHours ?? {}) as UpdateCompanySettingsRequest['businessHours'],
  };
  return {
    ...base,
    finanzOnlineApiUrl: v.finanzOnlineApiUrl,
    finanzOnlineUsername: v.finanzOnlineParticipantId,
    finanzOnlinePassword: v.finanzOnlinePin,
    finanzOnlineEnabled: v.finanzOnlineEnabled,
    finanzOnlineSubmitInterval: v.finanzOnlineSubmitInterval,
    finanzOnlineRetryAttempts: v.finanzOnlineRetryAttempts,
    finanzOnlineAutoSubmit: v.finanzOnlineAutoSubmit,
    finanzOnlineEnableValidation: v.finanzOnlineEnableValidation,
    defaultTseDeviceId: v.defaultTseDeviceId,
    tseAutoConnect: v.tseAutoConnect,
    tseConnectionTimeout: v.tseConnectionTimeout,
  } as UpdateCompanySettingsRequest;
}

/** Build a full PUT payload from partial form values + existing GET settings. */
export function buildUpdateCompanySettingsRequest(
  partial: Partial<SettingsFormValues>,
  existing: CompanySettings | undefined | null
): UpdateCompanySettingsRequest {
  return mapFormValuesToUpdateRequest(mergeSettingsFormForUpdate(partial, existing));
}
