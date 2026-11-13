import { describe, expect, it, jest } from '@jest/globals';

import { parsePosCompanyInfo } from '../services/api/companyService';

jest.mock('../services/api/config', () => ({
  apiClient: { get: jest.fn() },
}));

describe('parsePosCompanyInfo', () => {
  it('reads camelCase fields', () => {
    const info = parsePosCompanyInfo({
      companyName: 'Cafe GmbH',
      companyAddress: 'Wien 1',
      taxNumber: 'ATU12345678',
      receiptFooter: 'Danke!',
    });
    expect(info.companyName).toBe('Cafe GmbH');
    expect(info.companyAddress).toBe('Wien 1');
    expect(info.taxNumber).toBe('ATU12345678');
    expect(info.receiptFooter).toBe('Danke!');
  });

  it('reads PascalCase fields', () => {
    const info = parsePosCompanyInfo({
      CompanyName: 'Bar GmbH',
      CompanyAddress: 'Salzburg 2',
      TaxNumber: 'ATU87654321',
      ReceiptFooter: null,
    });
    expect(info.companyName).toBe('Bar GmbH');
    expect(info.taxNumber).toBe('ATU87654321');
    expect(info.receiptFooter).toBeNull();
  });
});
