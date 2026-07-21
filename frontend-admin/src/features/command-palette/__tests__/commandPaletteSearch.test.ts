import { describe, expect, it } from 'vitest';

import { looksLikeReceiptNumber } from '@/features/command-palette/commandPaletteSearch';

describe('commandPaletteSearch', () => {
  it('detects receipt-like numbers', () => {
    expect(looksLikeReceiptNumber('AT-1-20250101-42')).toBe(true);
    expect(looksLikeReceiptNumber('ab')).toBe(false);
  });
});
