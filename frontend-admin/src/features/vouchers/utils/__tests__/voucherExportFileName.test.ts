import { describe, expect, it } from 'vitest';

import { buildVoucherExportFileName } from '../voucherExportFileName';

describe('buildVoucherExportFileName', () => {
  it('builds json pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildVoucherExportFileName('cafe', 'json', at)).toBe(
      'voucher_cafe_20260722_143022.json'
    );
  });

  it('builds csv pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildVoucherExportFileName('cafe', 'csv', at)).toBe('voucher_cafe_20260722_143022.csv');
  });
});
