/**
 * Type-safe mapping between CompanySettings API response and UpdateCompanySettingsRequest.
 * Duplicate form field names are avoided (e.g. finanzOnlineParticipantId vs finanzOnlineUsername).
 */

import type { CompanySettings } from '@/api/generated/model';
import type { UpdateCompanySettingsRequest } from '@/api/generated/model';

export type SettingsFormValues = Omit<UpdateCompanySettingsRequest, 'businessHours'> & {
    businessHours?: Record<string, string>;
    finanzOnlineEnabled?: boolean;
    finanzOnlineParticipantId?: string;
    finanzOnlinePin?: string;
    finanzOnlineSubmitInterval?: number;
    finanzOnlineAutoSubmit?: boolean;
    defaultTseDeviceId?: string;
    tseAutoConnect?: boolean;
    tseConnectionTimeout?: number;
};

/** CompanySettings -> form values (API response uses different keys for some fields) */
export function mapSettingsToFormValues(s: CompanySettings | undefined): Partial<SettingsFormValues> {
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
        finanzOnlineParticipantId: s.finanzOnlineUsername ?? undefined,
        finanzOnlinePin: s.finanzOnlinePassword ?? undefined,
        finanzOnlineSubmitInterval: s.finanzOnlineSubmitInterval,
        finanzOnlineAutoSubmit: s.finanzOnlineAutoSubmit,
        defaultTseDeviceId: s.defaultTseDeviceId ?? undefined,
        tseAutoConnect: s.tseAutoConnect,
        tseConnectionTimeout: s.tseConnectionTimeout,
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
        finanzOnlineUsername: v.finanzOnlineParticipantId,
        finanzOnlinePassword: v.finanzOnlinePin,
        finanzOnlineEnabled: v.finanzOnlineEnabled,
        finanzOnlineSubmitInterval: v.finanzOnlineSubmitInterval,
        finanzOnlineAutoSubmit: v.finanzOnlineAutoSubmit,
        defaultTseDeviceId: v.defaultTseDeviceId,
        tseAutoConnect: v.tseAutoConnect,
        tseConnectionTimeout: v.tseConnectionTimeout,
    } as UpdateCompanySettingsRequest;
}
