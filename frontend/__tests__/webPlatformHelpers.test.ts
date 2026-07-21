import { describe, expect, it, jest } from '@jest/globals';

import { formatDateForHtmlInput } from '../utils/dateFormatter';
import { hexToRgba, createShadowStyle } from '../utils/shadowUtils';

jest.mock('react-native', () => ({
  Platform: { OS: 'web' },
}));

describe('shadowUtils hexToRgba', () => {
  it('expands short hex so web box-shadow is valid', () => {
    expect(hexToRgba('#000', 0.25)).toBe('rgba(0, 0, 0, 0.25)');
    expect(hexToRgba('#fff', 0.1)).toBe('rgba(255, 255, 255, 0.1)');
  });

  it('handles full hex', () => {
    expect(hexToRgba('#3D3229', 0.05)).toBe('rgba(61, 50, 41, 0.05)');
  });

  it('clamps alpha', () => {
    expect(hexToRgba('#000000', 2)).toBe('rgba(0, 0, 0, 1)');
    expect(hexToRgba('#000000', -1)).toBe('rgba(0, 0, 0, 0)');
  });
});

describe('createShadowStyle (web)', () => {
  it('emits rgba boxShadow instead of invalid short-hex alpha', () => {
    const style = createShadowStyle('#000', { width: 0, height: 2 }, 0.25, 3.84, 5) as {
      boxShadow?: string;
    };
    expect(style.boxShadow).toBe('0px 2px 3.84px 0px rgba(0, 0, 0, 0.25)');
    expect(style.boxShadow).not.toContain('#000');
  });
});

describe('formatDateForHtmlInput', () => {
  it('formats local date for HTML date input', () => {
    const d = new Date(2026, 6, 21, 14, 30, 0); // Jul 21 2026 14:30 local
    expect(formatDateForHtmlInput(d, 'date')).toBe('2026-07-21');
    expect(formatDateForHtmlInput(d, 'time')).toBe('14:30');
    expect(formatDateForHtmlInput(d, 'datetime')).toBe('2026-07-21T14:30');
  });
});
