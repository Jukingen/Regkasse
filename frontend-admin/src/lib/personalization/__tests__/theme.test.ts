import { describe, expect, it, vi } from 'vitest';

import { getSystemTheme, resolveEffectiveTheme } from '../theme';

describe('personalization theme', () => {
  it('resolveEffectiveTheme returns explicit modes', () => {
    expect(resolveEffectiveTheme('light')).toBe('light');
    expect(resolveEffectiveTheme('dark')).toBe('dark');
  });

  it('resolveEffectiveTheme follows system preference', () => {
    vi.stubGlobal('window', {
      matchMedia: (query: string) => ({
        matches: query.includes('dark'),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      }),
    });
    expect(getSystemTheme()).toBe('dark');
    expect(resolveEffectiveTheme('system')).toBe('dark');
    vi.unstubAllGlobals();
  });
});
