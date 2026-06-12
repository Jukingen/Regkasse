import { describe, expect, it } from 'vitest';
import { mapCompanyFormToUpdateRequest } from '@/features/settings/types/companySettingsForm';
import type { CompanySettings } from '@/api/generated/model';

describe('mapCompanyFormToUpdateRequest', () => {
    it('merges company fields with existing settings and shell defaults', () => {
        const existing = {
            companyName: 'Old GmbH',
            companyAddress: 'Old St 1',
            companyTaxNumber: 'ATU11111111',
            currency: 'EUR',
            language: 'de-DE',
            timeZone: 'Europe/Vienna',
            dateFormat: 'dd.MM.yyyy',
            timeFormat: 'HH:mm:ss',
            decimalPlaces: 2,
            defaultPaymentMethod: 'Cash',
            taxCalculationMethod: 'Standard',
            invoiceNumbering: 'Sequential',
            receiptNumbering: 'Sequential',
            finanzOnlineEnabled: true,
            finanzOnlineSubmitInterval: 30,
        } as CompanySettings;

        const payload = mapCompanyFormToUpdateRequest(
            {
                companyName: 'New GmbH',
                companyAddress: 'New St 2',
                companyTaxNumber: 'ATU22222222',
                companyPhone: '+43 1',
                companyEmail: 'info@new.at',
            },
            existing,
        );

        expect(payload.companyName).toBe('New GmbH');
        expect(payload.companyAddress).toBe('New St 2');
        expect(payload.companyTaxNumber).toBe('ATU22222222');
        expect(payload.companyPhone).toBe('+43 1');
        expect(payload.defaultCurrency).toBe('EUR');
        expect(payload.finanzOnlineEnabled).toBe(true);
        expect(payload.finanzOnlineSubmitInterval).toBe(30);
    });

    it('uses shell defaults when no row exists yet', () => {
        const payload = mapCompanyFormToUpdateRequest(
            {
                companyName: 'Startup GmbH',
                companyAddress: 'Wien',
                companyTaxNumber: 'ATU33333333',
            },
            null,
        );

        expect(payload.defaultCurrency).toBe('EUR');
        expect(payload.defaultLanguage).toBe('de-DE');
        expect(payload.defaultTimeZone).toBe('Europe/Vienna');
        expect(payload.receiptNumbering).toBe('Sequential');
    });
});
