import { describe, expect, it } from 'vitest';
import { mapApiToPersonalization, mapPersonalizationToApi } from '../userPreferencesApi';
import { DEFAULT_PERSONALIZATION } from '../types';

describe('userPreferencesApi mappers', () => {
  it('maps API response to personalization defaults', () => {
    const prefs = mapApiToPersonalization({
      themeMode: 'dark',
      densityMode: 'standard',
      defaultPage: '/admin/users',
      dateFormat: 'DD.MM.YYYY',
      timeFormat: '24h',
      reducedAnimations: true,
    });
    expect(prefs.themeMode).toBe('dark');
    expect(prefs.density).toBe('standard');
    expect(prefs.defaultLandingPath).toBe('/admin/users');
    expect(prefs.dateFormat).toBe('DD.MM.YYYY');
    expect(prefs.reducedAnimations).toBe(true);
  });

  it('maps personalization to API request', () => {
    const body = mapPersonalizationToApi({
      ...DEFAULT_PERSONALIZATION,
      themeMode: 'system',
      density: 'compact',
      dateFormat: 'YYYY-MM-DD',
    });
    expect(body.themeMode).toBe('system');
    expect(body.densityMode).toBe('compact');
    expect(body.dateFormat).toBe('YYYY-MM-DD');
  });
});
