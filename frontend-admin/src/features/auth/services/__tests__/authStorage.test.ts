import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  ACCESS_TOKEN_COOKIE_NAME,
  EDGE_SESSION_COOKIE_NAME,
  MAX_ACCESS_TOKEN_COOKIE_CHARS,
  authStorage,
  readAccessTokenCookie,
} from '../authStorage';

describe('authStorage cookie + localStorage mirror', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    document.cookie.split(';').forEach((part) => {
      const name = part.split('=')[0]?.trim();
      if (name) {
        document.cookie = `${name}=; Path=/; Max-Age=0`;
      }
    });
  });

  afterEach(() => {
    authStorage.removeToken();
  });

  it('setToken writes memory, localStorage, and proxy cookie', () => {
    const jwt = 'header.payload.signature';
    authStorage.setToken(jwt);

    expect(authStorage.getToken()).toBe(jwt);
    expect(window.localStorage.getItem('rk_admin_access_token')).toBe(jwt);
    expect(readAccessTokenCookie()).toBe(jwt);
    expect(document.cookie).toContain(`${ACCESS_TOKEN_COOKIE_NAME}=`);
    expect(document.cookie).toContain(`${EDGE_SESSION_COOKIE_NAME}=1`);
  });

  it('setTokens writes access cookie and refresh localStorage only', () => {
    authStorage.setTokens({
      accessToken: 'aaa.bbb.ccc',
      refreshToken: 'refresh-secret',
    });

    expect(authStorage.getToken()).toBe('aaa.bbb.ccc');
    expect(readAccessTokenCookie()).toBe('aaa.bbb.ccc');
    expect(authStorage.getRefreshToken()).toBe('refresh-secret');
    expect(document.cookie).not.toContain('refresh-secret');
  });

  it('skips oversized JWT cookie and still writes compact edge session cookie', () => {
    const hugeJwt = `hdr.${'a'.repeat(MAX_ACCESS_TOKEN_COOKIE_CHARS)}.sig`;
    authStorage.setToken(hugeJwt);

    expect(window.localStorage.getItem('rk_admin_access_token')).toBe(hugeJwt);
    expect(readAccessTokenCookie()).toBeNull();
    expect(document.cookie).toContain(`${EDGE_SESSION_COOKIE_NAME}=1`);
  });
});
