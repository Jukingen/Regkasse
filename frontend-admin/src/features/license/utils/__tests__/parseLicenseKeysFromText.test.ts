import { describe, expect, it } from 'vitest';

import { parseLicenseKeysFromText } from '@/features/license/utils/parseLicenseKeysFromText';

describe('parseLicenseKeysFromText', () => {
  it('reads REGK keys from CSV, skips headers, and dedupes', () => {
    const text = [
      'licenseKey,tenant',
      'REGK-20270101-cafe-A7F3K2D9,cafe',
      'REGK-20270101-cafe-A7F3K2D9,dup',
      'not-a-key',
      'REGK-20990101-system-ABCDEF12',
    ].join('\n');

    expect(parseLicenseKeysFromText(text)).toEqual([
      'REGK-20270101-cafe-A7F3K2D9',
      'REGK-20990101-system-ABCDEF12',
    ]);
  });
});
