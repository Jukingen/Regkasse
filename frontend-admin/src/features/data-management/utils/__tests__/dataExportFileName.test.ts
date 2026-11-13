import { describe, expect, it } from 'vitest';

import { buildDataExportFileName, sanitizeDataExportFileSegment } from '../dataExportFileName';

describe('buildDataExportFileName', () => {
  it('builds canonical pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildDataExportFileName('cafe', at)).toBe('data-export_cafe_20260722_143022.zip');
  });

  it('sanitizes slug', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildDataExportFileName('Cafe Alpha', at)).toBe(
      'data-export_Cafe_Alpha_20260722_143022.zip'
    );
  });
});

describe('sanitizeDataExportFileSegment', () => {
  it('falls back when empty', () => {
    expect(sanitizeDataExportFileSegment(null, 'tenant')).toBe('tenant');
  });
});
