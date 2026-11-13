import { describe, expect, it } from 'vitest';

import {
  buildLicensesExportFileName,
  buildSingleLicenseExportFileName,
} from '../licenseExportFileName';

describe('licenseExportFileName', () => {
  it('builds single txt pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildSingleLicenseExportFileName('cafe', at)).toBe('license_cafe_20260722_143022.txt');
  });

  it('builds multiple json pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildLicensesExportFileName('cafe', 'json', at)).toBe(
      'licenses_cafe_20260722_143022.json'
    );
  });

  it('builds multiple csv pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildLicensesExportFileName('cafe', 'csv', at)).toBe(
      'licenses_cafe_20260722_143022.csv'
    );
  });
});
