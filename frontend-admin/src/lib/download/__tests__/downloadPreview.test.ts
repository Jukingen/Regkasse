import { describe, expect, it } from 'vitest';

import {
  LARGE_EXPORT_BYTES,
  estimateTabularExportBytes,
  isLargeExport,
  resolvePreviewSizeBytes,
} from '@/lib/download/downloadPreview';

describe('downloadPreview helpers', () => {
  it('flags exports above 100 MB', () => {
    expect(isLargeExport(LARGE_EXPORT_BYTES)).toBe(false);
    expect(isLargeExport(LARGE_EXPORT_BYTES + 1)).toBe(true);
    expect(isLargeExport(undefined)).toBe(false);
  });

  it('estimates tabular export size by format', () => {
    expect(estimateTabularExportBytes(10, 'csv')).toBe(10 * 160 + 128);
    expect(estimateTabularExportBytes(10, 'json')).toBe(10 * 320 + 64);
    expect(estimateTabularExportBytes(-5, 'csv')).toBe(128);
  });

  it('prefers known bytes over estimate', () => {
    expect(resolvePreviewSizeBytes({ knownBytes: 2048, estimatedBytes: 999 })).toEqual({
      sizeBytes: 2048,
      isEstimate: false,
    });
    expect(resolvePreviewSizeBytes({ estimatedBytes: 512 })).toEqual({
      sizeBytes: 512,
      isEstimate: true,
    });
  });
});
