import {
  SETTINGS_REQUIRED_MESSAGE_KEYS,
} from '@/features/settings/constants/settingsFieldTabs';
import {
  isRequiredFieldValidationMessage,
} from '@/lib/form/applyAspNetFieldErrorsToForm';

type Translate = (key: string, options?: Record<string, string | number>) => string;

/** Form field → label i18n key (for requiredWithField fallback). */
const SETTINGS_FIELD_LABEL_KEYS: Record<string, string> = {
  companyName: 'settings.form.general.companyName',
  companyAddress: 'settings.form.general.companyAddress',
  companyTaxNumber: 'settings.form.general.companyTaxNumber',
  companyVatNumber: 'settings.form.general.companyVatNumber',
  contactPerson: 'settings.form.general.contactPerson',
  contactEmail: 'settings.form.general.contactEmail',
  contactPhone: 'settings.form.general.contactPhone',
  companyWebsite: 'settings.form.general.companyWebsite',
  bankName: 'settings.form.general.bankName',
  bankAccountNumber: 'settings.form.general.bankAccountNumber',
  bankSwiftCode: 'settings.form.general.bankSwiftCode',
  companyPhone: 'settings.companyPage.phone',
  companyEmail: 'settings.companyPage.email',
  companyDescription: 'settings.companyPage.receiptFooter',
  defaultLanguage: 'settings.form.localization.defaultLanguage',
  defaultCurrency: 'settings.form.localization.defaultCurrency',
  defaultTimeZone: 'settings.form.localization.defaultTimeZone',
  defaultDateFormat: 'settings.form.localization.defaultDateFormat',
  defaultTimeFormat: 'settings.form.localization.defaultTimeFormat',
  receiptNumbering: 'settings.form.localization.receiptNumbering',
  invoiceNumbering: 'settings.form.localization.invoiceNumbering',
  defaultPaymentMethod: 'settings.form.localization.defaultPaymentMethod',
  taxCalculationMethod: 'settings.form.localization.taxCalculationMethod',
  finanzOnlineApiUrl: 'settings.form.finanzOnline.apiUrl',
  finanzOnlineParticipantId: 'settings.form.finanzOnline.participantId',
  finanzOnlinePin: 'settings.form.finanzOnline.pin',
  finanzOnlineSubmitInterval: 'settings.form.finanzOnline.sessionTimeout',
  finanzOnlineRetryAttempts: 'settings.form.finanzOnline.retryAttempts',
  defaultTseDeviceId: 'settings.form.tse.defaultDeviceId',
  tseConnectionTimeout: 'settings.form.tse.connectionTimeout',
};

/** Field-specific non-required validation keys (pattern / format). */
const SETTINGS_FIELD_FORMAT_KEYS: Record<string, string> = {
  companyTaxNumber: 'settings.form.general.companyTaxNumberPattern',
  companyVatNumber: 'settings.form.general.companyTaxNumberPattern',
  contactEmail: 'settings.form.general.contactEmailInvalid',
  companyEmail: 'settings.form.general.contactEmailInvalid',
  finanzOnlineApiUrl: 'settings.form.finanzOnline.apiUrlInvalid',
};

function looksLikeAspNetEnglishMessage(message: string): boolean {
  return (
    /^The .+ field is required\.?$/i.test(message) ||
    /^The .+ field is not a valid e-?mail address\.?$/i.test(message) ||
    /^The .+ field is not a valid fully-qualified http, https, or ftp URL\.?$/i.test(message) ||
    /maximum length of ['"]?\d+['"]?/i.test(message) ||
    /minimum length of ['"]?\d+['"]?/i.test(message) ||
    /must be between/i.test(message) ||
    /is not valid/i.test(message)
  );
}

function extractMaxLength(message: string): number | undefined {
  const m =
    message.match(/maximum length of ['"]?(\d+)['"]?/i) ??
    message.match(/max(?:imum)?(?: length)?[:\s]+(\d+)/i);
  return m ? Number(m[1]) : undefined;
}

function extractMinLength(message: string): number | undefined {
  const m =
    message.match(/minimum length of ['"]?(\d+)['"]?/i) ??
    message.match(/min(?:imum)?(?: length)?[:\s]+(\d+)/i);
  return m ? Number(m[1]) : undefined;
}

function extractRange(message: string): { min: number; max: number } | undefined {
  const m =
    message.match(/between\s+(-?\d+(?:\.\d+)?)\s+and\s+(-?\d+(?:\.\d+)?)/i) ??
    message.match(/must be between\s+(-?\d+(?:\.\d+)?)\s+and\s+(-?\d+(?:\.\d+)?)/i);
  if (!m) return undefined;
  return { min: Number(m[1]), max: Number(m[2]) };
}

function fieldLabel(t: Translate, formField: string): string {
  const key = SETTINGS_FIELD_LABEL_KEYS[formField];
  return key ? t(key) : formField;
}

/**
 * Translate ASP.NET / ProblemDetails field validation messages into the active UI locale.
 * Prefer field-specific settings keys; otherwise map common DataAnnotations patterns to common.validation.*.
 */
export function localizeSettingsFieldValidationMessages(
  t: Translate,
  formField: string,
  messages: string[]
): string[] {
  if (!messages.length) return messages;

  return messages.map((raw) => {
    const message = raw.trim();
    if (!message) return message;

    // Required
    if (isRequiredFieldValidationMessage(message)) {
      const requiredKey = SETTINGS_REQUIRED_MESSAGE_KEYS[formField];
      if (requiredKey) return t(requiredKey);
      return t('common.validation.requiredWithField', { field: fieldLabel(t, formField) });
    }

    // Email
    if (/e-?mail/i.test(message) && /valid|ungueltig|gecersiz|ungültig/i.test(message)) {
      const formatKey = SETTINGS_FIELD_FORMAT_KEYS[formField];
      if (formatKey && /email|contactEmail|companyEmail/i.test(formField)) {
        return t(formatKey);
      }
      return t('common.validation.emailInvalid');
    }

    // URL
    if (/url|http|https|ftp/i.test(message) && /valid|ungueltig|gecersiz|ungültig/i.test(message)) {
      const formatKey = SETTINGS_FIELD_FORMAT_KEYS[formField];
      if (formatKey) return t(formatKey);
      return t('common.validation.urlInvalid');
    }

    // ATU / tax pattern (when backend sends generic invalid)
    if (
      (formField === 'companyTaxNumber' || formField === 'companyVatNumber') &&
      (/ATU|tax|UID|pattern|format|invalid/i.test(message) || looksLikeAspNetEnglishMessage(message))
    ) {
      return t('settings.form.general.companyTaxNumberPattern');
    }

    // Max length
    const maxLen = extractMaxLength(message);
    if (maxLen != null && !Number.isNaN(maxLen)) {
      return t('common.validation.maxLength', { max: maxLen });
    }

    // Min length
    const minLen = extractMinLength(message);
    if (minLen != null && !Number.isNaN(minLen)) {
      return t('common.validation.minLength', { min: minLen });
    }

    // Range
    const range = extractRange(message);
    if (range) {
      return t('common.validation.numberBetween', { min: range.min, max: range.max });
    }

    // Known English ASP.NET leftovers → generic localized invalid
    if (looksLikeAspNetEnglishMessage(message)) {
      return t('common.validation.invalidValue');
    }

    // Already localized (or unknown custom message) — keep as-is
    return message;
  });
}
