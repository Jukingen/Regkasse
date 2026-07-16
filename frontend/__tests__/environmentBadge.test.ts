import { afterEach, describe, expect, it, jest } from '@jest/globals';

describe('getEnvironmentBadge', () => {
  const originalDev = (global as typeof globalThis & { __DEV__?: boolean }).__DEV__;

  afterEach(() => {
    (global as typeof globalThis & { __DEV__?: boolean }).__DEV__ = originalDev;
    jest.resetModules();
  });

  it('returns Entwicklung badge text when __DEV__ is true', () => {
    (global as typeof globalThis & { __DEV__?: boolean }).__DEV__ = true;
    jest.resetModules();
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const { getEnvironmentBadge } = require('../shared/config/environmentBadge') as typeof import('../shared/config/environmentBadge');
    const badge = getEnvironmentBadge();
    expect(badge.type).toBe('development');
    expect(badge.text).toContain('Entwicklung');
    expect(badge.text.length).toBeGreaterThan(0);
  });

  it('returns empty badge text when __DEV__ is false', () => {
    (global as typeof globalThis & { __DEV__?: boolean }).__DEV__ = false;
    jest.resetModules();
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const { getEnvironmentBadge } = require('../shared/config/environmentBadge') as typeof import('../shared/config/environmentBadge');
    const badge = getEnvironmentBadge();
    expect(badge.type).toBe('production');
    expect(badge.text).toBe('');
  });
});
