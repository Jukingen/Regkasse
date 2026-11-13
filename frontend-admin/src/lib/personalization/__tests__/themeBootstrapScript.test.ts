import { describe, expect, it } from 'vitest';

import { PERSONALIZATION_STORAGE_KEY, THEME_MODE_STORAGE_KEY } from '@/lib/personalization/storage';
import { THEME_BOOTSTRAP_SCRIPT } from '@/lib/personalization/themeBootstrapScript';

const SENSITIVE_PATTERNS = [
  /Bearer\s+/i,
  /api[_-]?key/i,
  /secret/i,
  /password/i,
  /refreshToken/i,
  /accessToken/i,
  /NEXT_PUBLIC_API/,
  /localStorage\.setItem/,
];

describe('THEME_BOOTSTRAP_SCRIPT', () => {
  it('embeds storage keys via JSON.stringify (safe literal injection)', () => {
    expect(THEME_BOOTSTRAP_SCRIPT).toContain(JSON.stringify(THEME_MODE_STORAGE_KEY));
    expect(THEME_BOOTSTRAP_SCRIPT).toContain(JSON.stringify(PERSONALIZATION_STORAGE_KEY));
  });

  it('only reads localStorage and does not write secrets', () => {
    expect(THEME_BOOTSTRAP_SCRIPT).toContain('localStorage.getItem');
    for (const pattern of SENSITIVE_PATTERNS) {
      expect(THEME_BOOTSTRAP_SCRIPT).not.toMatch(pattern);
    }
  });

  it('is an IIFE that mutates documentElement theme/density attributes', () => {
    expect(THEME_BOOTSTRAP_SCRIPT.startsWith('(function(){')).toBe(true);
    expect(THEME_BOOTSTRAP_SCRIPT).toContain("setAttribute('data-theme'");
    expect(THEME_BOOTSTRAP_SCRIPT).toContain("setAttribute('data-density'");
    expect(THEME_BOOTSTRAP_SCRIPT).toContain('prefers-color-scheme');
  });
});
