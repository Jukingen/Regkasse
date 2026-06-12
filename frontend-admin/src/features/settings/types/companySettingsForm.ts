import type { CompanySettings } from '@/api/generated/model';
import type { UpdateCompanySettingsRequest } from '@/api/generated/model';
import {
    mapFormValuesToUpdateRequest,
    mapSettingsToFormValues,
    type SettingsFormValues,
} from '@/features/settings/types/settingsForm';

/** RKSV company profile fields edited on `/settings/company`. */
export type CompanySettingsFormValues = Pick<
    SettingsFormValues,
    | 'companyName'
    | 'companyAddress'
    | 'companyTaxNumber'
    | 'companyPhone'
    | 'companyEmail'
    | 'companyWebsite'
    | 'companyDescription'
>;

const SHELL_DEFAULTS: Pick<
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

export function mapCompanySettingsToFormValues(
    settings: CompanySettings | undefined | null,
): Partial<CompanySettingsFormValues> {
    const mapped = mapSettingsToFormValues(settings ?? undefined);
    return {
        companyName: mapped.companyName,
        companyAddress: mapped.companyAddress,
        companyTaxNumber: mapped.companyTaxNumber,
        companyPhone: mapped.companyPhone,
        companyEmail: mapped.companyEmail,
        companyWebsite: mapped.companyWebsite,
        companyDescription: mapped.companyDescription,
    };
}

/** Merge company-only form values with existing tenant settings for a full PUT payload. */
export function mapCompanyFormToUpdateRequest(
    form: CompanySettingsFormValues,
    existing: CompanySettings | undefined | null,
): UpdateCompanySettingsRequest {
    const base = mapSettingsToFormValues(existing ?? undefined) as SettingsFormValues;
    const merged: SettingsFormValues = {
        ...SHELL_DEFAULTS,
        ...base,
        ...form,
        companyName: form.companyName ?? base.companyName ?? '',
        companyAddress: form.companyAddress ?? base.companyAddress ?? '',
        companyTaxNumber: form.companyTaxNumber ?? base.companyTaxNumber ?? '',
        defaultCurrency: base.defaultCurrency ?? SHELL_DEFAULTS.defaultCurrency!,
        defaultLanguage: base.defaultLanguage ?? SHELL_DEFAULTS.defaultLanguage!,
        defaultTimeZone: base.defaultTimeZone ?? SHELL_DEFAULTS.defaultTimeZone!,
        defaultDateFormat: base.defaultDateFormat ?? SHELL_DEFAULTS.defaultDateFormat!,
        defaultTimeFormat: base.defaultTimeFormat ?? SHELL_DEFAULTS.defaultTimeFormat!,
        defaultDecimalPlaces: base.defaultDecimalPlaces ?? SHELL_DEFAULTS.defaultDecimalPlaces!,
        defaultPaymentMethod: base.defaultPaymentMethod ?? SHELL_DEFAULTS.defaultPaymentMethod!,
        taxCalculationMethod: base.taxCalculationMethod ?? SHELL_DEFAULTS.taxCalculationMethod!,
        invoiceNumbering: base.invoiceNumbering ?? SHELL_DEFAULTS.invoiceNumbering!,
        receiptNumbering: base.receiptNumbering ?? SHELL_DEFAULTS.receiptNumbering!,
        businessHours: base.businessHours ?? {},
    };
    return mapFormValuesToUpdateRequest(merged);
}
