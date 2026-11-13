/**
 * Settings hub field → Ant Design Tabs activeKey mapping (SuperAdminSettings).
 * Used to jump to the tab that owns a validation error.
 */
export const SETTINGS_HUB_FIELD_TAB: Record<string, string> = {
  // General (tab 1)
  companyName: '1',
  companyAddress: '1',
  companyTaxNumber: '1',
  companyVatNumber: '1',
  contactPerson: '1',
  contactEmail: '1',
  contactPhone: '1',
  companyWebsite: '1',
  bankName: '1',
  bankAccountNumber: '1',
  bankSwiftCode: '1',
  bankRoutingNumber: '1',
  paymentTerms: '1',
  companyPhone: '1',
  companyEmail: '1',
  companyDescription: '1',
  companyLogo: '1',
  companyRegistrationNumber: '1',

  // Localization (tab 2)
  defaultLanguage: '2',
  defaultCurrency: '2',
  defaultTimeZone: '2',
  defaultDateFormat: '2',
  defaultTimeFormat: '2',
  defaultDecimalPlaces: '2',
  receiptNumbering: '2',
  invoiceNumbering: '2',
  defaultPaymentMethod: '2',
  taxCalculationMethod: '2',

  // FinanzOnline (tab 3)
  finanzOnlineEnabled: '3',
  finanzOnlineApiUrl: '3',
  finanzOnlineParticipantId: '3',
  finanzOnlinePin: '3',
  finanzOnlineSubmitInterval: '3',
  finanzOnlineAutoSubmit: '3',
  finanzOnlineRetryAttempts: '3',
  finanzOnlineEnableValidation: '3',

  // TSE (tab 4)
  tseAutoConnect: '4',
  defaultTseDeviceId: '4',
  tseConnectionTimeout: '4',
};

/** i18n keys for required-field copy (prefer over English ASP.NET messages). */
export const SETTINGS_REQUIRED_MESSAGE_KEYS: Record<string, string> = {
  companyName: 'settings.form.general.companyNameRequired',
  companyAddress: 'settings.form.general.companyAddressRequired',
  companyTaxNumber: 'settings.form.general.companyTaxNumberRequired',
  defaultLanguage: 'settings.form.localization.defaultLanguageRequired',
  defaultCurrency: 'settings.form.localization.defaultCurrencyRequired',
  defaultTimeZone: 'settings.form.localization.defaultTimeZoneRequired',
  defaultDateFormat: 'settings.form.localization.defaultDateFormatRequired',
  defaultTimeFormat: 'settings.form.localization.defaultTimeFormatRequired',
  receiptNumbering: 'settings.form.localization.receiptNumberingRequired',
  invoiceNumbering: 'settings.form.localization.invoiceNumberingRequired',
  defaultPaymentMethod: 'settings.form.localization.defaultPaymentMethodRequired',
  taxCalculationMethod: 'settings.form.localization.taxCalculationMethodRequired',
};

export function resolveSettingsHubTabForField(fieldName: string): string | undefined {
  return SETTINGS_HUB_FIELD_TAB[fieldName];
}
