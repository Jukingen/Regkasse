import { describe, expect, it } from 'vitest';

import { buildAuditExportFileName, normalizeAuditExportExtension } from '../auditExportFileName';

describe('buildAuditExportFileName', () => {
  it('builds json pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(
      buildAuditExportFileName({
        tenantSlug: 'cafe',
        fromDate: '2026-07-01',
        toDate: '2026-07-22',
        format: 'json',
        at,
      })
    ).toBe('audit_cafe_20260701_20260722_20260722_143022.json');
  });

  it('builds csv pattern and maps excel to csv', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(
      buildAuditExportFileName({
        tenantSlug: 'cafe',
        fromDate: new Date(2026, 6, 1),
        toDate: new Date(2026, 6, 22),
        format: 'excel',
        at,
      })
    ).toBe('audit_cafe_20260701_20260722_20260722_143022.csv');
  });

  it('uses all when dates missing', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildAuditExportFileName({ tenantSlug: 'cafe', format: 'csv', at })).toBe(
      'audit_cafe_all_all_20260722_143022.csv'
    );
  });
});

describe('normalizeAuditExportExtension', () => {
  it('maps formats', () => {
    expect(normalizeAuditExportExtension('json')).toBe('json');
    expect(normalizeAuditExportExtension('excel')).toBe('csv');
  });
});
