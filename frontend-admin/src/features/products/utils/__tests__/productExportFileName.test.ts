import { describe, expect, it } from 'vitest';

import { buildProductExportFileName } from '../productExportFileName';

describe('buildProductExportFileName', () => {
  it('builds csv pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildProductExportFileName('cafe', 'csv', at)).toBe('product_cafe_20260722_143022.csv');
  });

  it('builds json pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildProductExportFileName('cafe', 'json', at)).toBe(
      'product_cafe_20260722_143022.json'
    );
  });
});
