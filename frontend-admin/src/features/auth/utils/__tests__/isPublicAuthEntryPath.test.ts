import { afterEach, describe, expect, it, vi } from 'vitest';

import { isPublicAuthEntryPath } from '@/features/auth/utils/isPublicAuthEntryPath';

describe('isPublicAuthEntryPath', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns false when window is undefined (SSR)', () => {
    vi.stubGlobal('window', undefined);
    expect(isPublicAuthEntryPath()).toBe(false);
  });

  it('returns true for /login and nested login paths', () => {
    vi.stubGlobal('window', { location: { pathname: '/login' } });
    expect(isPublicAuthEntryPath()).toBe(true);
    vi.stubGlobal('window', { location: { pathname: '/login/forgot-username' } });
    expect(isPublicAuthEntryPath()).toBe(true);
  });

  it('returns false for protected paths', () => {
    vi.stubGlobal('window', { location: { pathname: '/dashboard' } });
    expect(isPublicAuthEntryPath()).toBe(false);
  });
});
