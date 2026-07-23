import { describe, expect, it } from 'vitest';

import { buildLogExportFileName } from '../logExportFileName';

describe('buildLogExportFileName', () => {
  it('builds txt pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildLogExportFileName('cafe', 'txt', at)).toBe('log_cafe_20260722_143022.txt');
  });

  it('builds csv and json', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildLogExportFileName('cafe', 'csv', at)).toBe('log_cafe_20260722_143022.csv');
    expect(buildLogExportFileName('cafe', 'json', at)).toBe('log_cafe_20260722_143022.json');
  });
});
