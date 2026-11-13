import { describe, expect, it } from 'vitest';

import type { CompanySettings } from '@/api/generated/model';
import {
  buildUpdateCompanySettingsRequest,
  mergeSettingsFormForUpdate,
} from '@/features/settings/types/settingsForm';

const existing = {
  companyName: 'Development Company',
  companyAddress: 'Development Address',
  companyTaxNumber: 'ATU00000000',
  companyVatNumber: 'ATU00000000',
  companyWebsite: 'www.dev.com',
  contactPerson: 'Default Contact',
  contactPhone: 'Default Phone',
  contactEmail: 'contact@defaultcompany.com',
  bankName: 'Default Bankname',
  bankAccountNumber: '456456456',
  bankSwiftCode: 'BIC-Default',
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

describe('mergeSettingsFormForUpdate', () => {
  it('keeps required localization fields when only general-tab values are submitted', () => {
    const merged = mergeSettingsFormForUpdate(
      {
        companyName: 'Development Company',
        companyAddress: 'Development Address',
        companyWebsite: 'www.dev.com',
        companyTaxNumber: 'ATU00000000',
        companyVatNumber: 'ATU00000000',
        contactPerson: 'Default Contact',
        contactPhone: 'Default Phone',
        contactEmail: 'contact@defaultcompany.com',
        bankName: 'Default Bankname',
        bankAccountNumber: '456456456',
        bankSwiftCode: 'BIC-Default',
      },
      existing
    );

    expect(merged.defaultCurrency).toBe('EUR');
    expect(merged.defaultLanguage).toBe('de-DE');
    expect(merged.defaultTimeZone).toBe('Europe/Vienna');
    expect(merged.defaultDateFormat).toBe('dd.MM.yyyy');
    expect(merged.defaultTimeFormat).toBe('HH:mm:ss');
    expect(merged.defaultPaymentMethod).toBe('Cash');
    expect(merged.taxCalculationMethod).toBe('Standard');
    expect(merged.invoiceNumbering).toBe('Sequential');
    expect(merged.receiptNumbering).toBe('Sequential');
    expect(merged.finanzOnlineEnabled).toBe(true);
  });
});

describe('buildUpdateCompanySettingsRequest', () => {
  it('builds a complete PUT body from a general-tab-only partial', () => {
    const payload = buildUpdateCompanySettingsRequest(
      {
        companyName: 'Renamed GmbH',
        companyAddress: 'New Address',
        companyTaxNumber: 'ATU00000000',
        contactPerson: 'Default Contact',
      },
      existing
    );

    expect(payload.companyName).toBe('Renamed GmbH');
    expect(payload.defaultCurrency).toBe('EUR');
    expect(payload.defaultLanguage).toBe('de-DE');
    expect(payload.defaultTimeZone).toBe('Europe/Vienna');
    expect(payload.invoiceNumbering).toBe('Sequential');
    expect(payload.receiptNumbering).toBe('Sequential');
    expect(payload.defaultPaymentMethod).toBe('Cash');
    expect(payload.taxCalculationMethod).toBe('Standard');
    expect(payload.finanzOnlineEnabled).toBe(true);
  });
});
