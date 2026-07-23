import { beforeEach, describe, expect, it } from 'vitest';

import {
  PERSONALIZATION_STORAGE_KEY,
  normalizePersonalization,
  readStoredPersonalization,
  writeStoredPersonalization,
} from '../storage';

describe('personalization storage', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('normalizes invalid payloads to defaults', () => {
    expect(normalizePersonalization(null).themeMode).toBe('system');
    expect(normalizePersonalization({ themeMode: 'neon', density: 'huge' }).density).toBe(
      'standard'
    );
    expect(normalizePersonalization({ defaultLandingPath: '/unknown' }).defaultLandingPath).toBe(
      '/dashboard'
    );
  });

  it('round-trips preferences in localStorage', () => {
    writeStoredPersonalization({
      themeMode: 'dark',
      density: 'compact',
      defaultLandingPath: '/reporting',
      dateFormat: 'YYYY-MM-DD',
      timeFormat: '12h',
      timeZone: 'Europe/Berlin',
      language: 'en',
      reducedAnimations: true,
    });
    expect(window.localStorage.getItem(PERSONALIZATION_STORAGE_KEY)).toBeTruthy();
    expect(readStoredPersonalization()).toMatchObject({
      themeMode: 'dark',
      density: 'compact',
      defaultLandingPath: '/reporting',
      dateFormat: 'YYYY-MM-DD',
      timeFormat: '12h',
      timeZone: 'Europe/Berlin',
      language: 'en',
      reducedAnimations: true,
    });
  });

  it('keeps supported date formats', () => {
    expect(normalizePersonalization({ dateFormat: 'MM/DD/YYYY' }).dateFormat).toBe('MM/DD/YYYY');
    expect(normalizePersonalization({ dateFormat: 'bogus' }).dateFormat).toBe('DD.MM.YYYY');
  });
});
