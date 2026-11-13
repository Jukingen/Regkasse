import { afterEach, describe, expect, it, vi } from 'vitest';

import { csvRow, downloadCsvText, escapeCsvCell, rowsToCsv } from '@/shared/utils/csv';

describe('csv helpers', () => {
  it('escapes commas, quotes, and newlines', () => {
    expect(escapeCsvCell(null)).toBe('');
    expect(escapeCsvCell(undefined)).toBe('');
    expect(escapeCsvCell('plain')).toBe('plain');
    expect(escapeCsvCell('a,b')).toBe('"a,b"');
    expect(escapeCsvCell('say "hi"')).toBe('"say ""hi"""');
    expect(escapeCsvCell('line\nbreak')).toBe('"line\nbreak"');
  });

  it('builds rows and multi-line CSV', () => {
    expect(csvRow(['a', 'b,c', 3])).toBe('a,"b,c",3');
    expect(rowsToCsv([['h1', 'h2'], ['v1', 'v2']])).toBe('h1,h2\nv1,v2');
  });

  it('downloads CSV via object URL (browser)', () => {
    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    const click = vi.fn();
    vi.stubGlobal('URL', { createObjectURL, revokeObjectURL });
    const createElement = vi.spyOn(document, 'createElement').mockReturnValue({
      href: '',
      download: '',
      click,
    } as unknown as HTMLAnchorElement);

    downloadCsvText('a,b\n1,2', 'export.csv');

    expect(createObjectURL).toHaveBeenCalled();
    expect(click).toHaveBeenCalled();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock');
    createElement.mockRestore();
    vi.unstubAllGlobals();
  });
});
