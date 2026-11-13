import { describe, expect, it } from 'vitest';

import { buildDepExportFileName, sanitizeDepExportFileSegment } from '../depExportFileName';

describe('buildDepExportFileName', () => {
  it('uses canonical pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildDepExportFileName('cafe', 'k1', at)).toBe(
      'dep-export_cafe_k1_20260722_143022.json'
    );
  });

  it('sanitizes unsafe characters', () => {
    const at = new Date(2026, 0, 2, 3, 4, 5);
    expect(buildDepExportFileName('cafe/beispiel', 'KASSE 001', at)).toBe(
      'dep-export_cafe_beispiel_KASSE_001_20260102_030405.json'
    );
  });
});

describe('sanitizeDepExportFileSegment', () => {
  it('falls back when empty', () => {
    expect(sanitizeDepExportFileSegment(null, 'tenant')).toBe('tenant');
    expect(sanitizeDepExportFileSegment('***', 'tenant')).toBe('tenant');
  });
});
