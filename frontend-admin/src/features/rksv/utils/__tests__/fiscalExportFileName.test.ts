import { describe, expect, it } from 'vitest';

import { buildFiscalExportFileName, sanitizeFiscalExportFileSegment } from '../fiscalExportFileName';

describe('buildFiscalExportFileName', () => {
  it('uses register pattern without profile', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(
      buildFiscalExportFileName({
        tenantSlug: 'cafe',
        registerNumber: 'k1',
        at,
      })
    ).toBe('fiscal-export_cafe_k1_20260722_143022.json');
  });

  it('inserts profile when provided', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(
      buildFiscalExportFileName({
        tenantSlug: 'cafe',
        registerNumber: 'k1',
        profileName: 'operational_preview',
        at,
      })
    ).toBe('fiscal-export_cafe_k1_operational_preview_20260722_143022.json');
  });
});

describe('sanitizeFiscalExportFileSegment', () => {
  it('falls back when empty', () => {
    expect(sanitizeFiscalExportFileSegment(null, 'tenant')).toBe('tenant');
  });
});
