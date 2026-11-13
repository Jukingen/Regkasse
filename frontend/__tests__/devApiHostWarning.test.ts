import { describe, expect, it } from '@jest/globals';

import {
  isLoopbackApiBaseUrl,
  shouldShowDevLoopbackApiWarning,
} from '../utils/devApiHostWarning';

describe('dev loopback API warning', () => {
  it('detects localhost and loopback hosts', () => {
    expect(isLoopbackApiBaseUrl('http://localhost:5184/api')).toBe(true);
    expect(isLoopbackApiBaseUrl('http://127.0.0.1:5184/api')).toBe(true);
    expect(isLoopbackApiBaseUrl('http://192.168.1.10:5184/api')).toBe(false);
  });

  it('warns on native/dev loopback and skips web', () => {
    expect(
      shouldShowDevLoopbackApiWarning({
        isDev: true,
        platformOS: 'android',
        apiBaseUrl: 'http://localhost:5184/api',
      })
    ).toBe(true);
    expect(
      shouldShowDevLoopbackApiWarning({
        isDev: true,
        platformOS: 'ios',
        apiBaseUrl: 'http://localhost:5184/api',
      })
    ).toBe(true);
    expect(
      shouldShowDevLoopbackApiWarning({
        isDev: true,
        platformOS: 'web',
        apiBaseUrl: 'http://localhost:5184/api',
      })
    ).toBe(false);
    expect(
      shouldShowDevLoopbackApiWarning({
        isDev: false,
        platformOS: 'android',
        apiBaseUrl: 'http://localhost:5184/api',
      })
    ).toBe(false);
  });
});
