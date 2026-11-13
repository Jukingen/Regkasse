import { describe, expect, it } from 'vitest';

import { buildCustomerExportFileName } from '../customerExportFileName';

describe('buildCustomerExportFileName', () => {
  it('builds csv pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildCustomerExportFileName('cafe', 'csv', at)).toBe(
      'customer_cafe_20260722_143022.csv'
    );
  });

  it('builds json pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildCustomerExportFileName('cafe', 'json', at)).toBe(
      'customer_cafe_20260722_143022.json'
    );
  });
});
