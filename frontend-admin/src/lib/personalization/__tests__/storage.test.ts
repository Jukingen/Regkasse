import { beforeEach, describe, expect, it } from 'vitest';
import {
  normalizePersonalization,
  PERSONALIZATION_STORAGE_KEY,
  readStoredPersonalization,
  writeStoredPersonalization,
} from '../storage';

describe('personalization storage', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('normalizes invalid payloads to defaults', () => {
    expect(normalizePersonalization(null).themeMode).toBe('system');
    expect(normalizePersonalization({ themeMode: 'neon', density: 'huge' }).density).toBe('standard');
    expect(normalizePersonalization({ defaultLandingPath: '/unknown' }).defaultLandingPath).toBe('/dashboard');
  });

  it('round-trips preferences in localStorage', () => {
    writeStoredPersonalization({
      themeMode: 'dark',
      density: 'compact',
      defaultLandingPath: '/reporting',
      dateFormat: 'MM/DD/YYYY',
      timeFormat: '12h',
      reducedAnimations: true,
    });
    expect(window.localStorage.getItem(PERSONALIZATION_STORAGE_KEY)).toBeTruthy();
    expect(readStoredPersonalization()).toMatchObject({
      themeMode: 'dark',
      density: 'compact',
      defaultLandingPath: '/reporting',
      dateFormat: 'MM/DD/YYYY',
      timeFormat: '12h',
      reducedAnimations: true,
    });
  });
});
