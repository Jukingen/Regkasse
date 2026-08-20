import { afterEach, describe, expect, it } from 'vitest';

import { CHANGE_PASSWORD_PATH } from '@/features/auth/constants/changePasswordRoute';
import { resolvePostLoginPath } from '@/features/auth/utils/postLoginPath';

describe('resolvePostLoginPath', () => {
  afterEach(() => {
    window.localStorage.clear();
  });

  it('returns force-password-change when required', () => {
    expect(resolvePostLoginPath(true)).toBe(CHANGE_PASSWORD_PATH);
  });

  it('defaults to /dashboard', () => {
    expect(resolvePostLoginPath(false)).toBe('/dashboard');
  });
});
