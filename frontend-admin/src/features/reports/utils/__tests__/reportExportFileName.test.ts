import { describe, expect, it } from 'vitest';

import { buildReportFileName, normalizeReportTypeLabel, periodForReportType } from '../reportExportFileName';

describe('buildReportFileName', () => {
  it('builds tagesbericht pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(
      buildReportFileName({
        reportType: 'tagesabschluss',
        tenantSlug: 'cafe',
        period: '20260722',
        at,
      })
    ).toBe('report_tagesbericht_cafe_20260722_20260722_143022.pdf');
  });

  it('builds monatsbericht pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(
      buildReportFileName({
        reportType: 'monatsbeleg',
        tenantSlug: 'cafe',
        businessDate: new Date(2026, 6, 1),
        at,
      })
    ).toBe('report_monatsbericht_cafe_202607_20260722_143022.pdf');
  });

  it('builds jahresbericht pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(
      buildReportFileName({
        reportType: 'jahresbeleg',
        tenantSlug: 'cafe',
        businessDate: new Date(2026, 0, 1),
        at,
      })
    ).toBe('report_jahresbericht_cafe_2026_20260722_143022.pdf');
  });
});

describe('normalizeReportTypeLabel', () => {
  it('maps closing types', () => {
    expect(normalizeReportTypeLabel('tagesabschluss')).toBe('tagesbericht');
    expect(normalizeReportTypeLabel('monatsbeleg')).toBe('monatsbericht');
    expect(normalizeReportTypeLabel('jahresbeleg')).toBe('jahresbericht');
  });
});

describe('periodForReportType', () => {
  it('formats day month year', () => {
    const day = new Date(2026, 6, 22);
    expect(periodForReportType('tagesbericht', day)).toBe('20260722');
    expect(periodForReportType('monatsbericht', day)).toBe('202607');
    expect(periodForReportType('jahresbericht', day)).toBe('2026');
  });
});
