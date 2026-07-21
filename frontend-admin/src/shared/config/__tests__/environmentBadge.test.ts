import { describe, expect, it } from 'vitest';

import {
  getEnvironmentBadge,
  readEnvironmentSnapshot,
} from '../../../../../shared/constants/environment';

describe('environment badge config', () => {
  it('returns Entwicklung badge for development snapshot', () => {
    const badge = getEnvironmentBadge({
      isDevelopment: true,
      isTest: false,
      isProduction: false,
    });
    expect(badge).toEqual({ text: '🧪 Entwicklung', color: 'orange' });
  });

  it('returns TEST badge when RKSV env is TEST', () => {
    const badge = getEnvironmentBadge({
      isDevelopment: false,
      isTest: true,
      isProduction: false,
    });
    expect(badge).toEqual({ text: '🧪 TEST', color: 'blue' });
  });

  it('returns null for production snapshot', () => {
    expect(
      getEnvironmentBadge({
        isDevelopment: false,
        isTest: false,
        isProduction: true,
      })
    ).toBeNull();
  });

  it('readEnvironmentSnapshot respects overrides', () => {
    expect(
      readEnvironmentSnapshot({ isDevelopment: true, isTest: true, isProduction: false })
    ).toEqual({ isDevelopment: true, isTest: true, isProduction: false });
  });
});
