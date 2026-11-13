import { describe, expect, it } from 'vitest';

import { decodeJwtPayload, isTruthyJwtClaim } from '@/features/auth/lib/jwtPayload';

function makeToken(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
  const body = btoa(JSON.stringify(payload))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
  return `${header}.${body}.sig`;
}

describe('jwtPayload', () => {
  it('decodes payload and strips Bearer prefix', () => {
    const token = makeToken({ tenant_id: 't1', sub: 'u1' });
    expect(decodeJwtPayload(token)).toEqual({ tenant_id: 't1', sub: 'u1' });
    expect(decodeJwtPayload(`Bearer ${token}`)).toEqual({ tenant_id: 't1', sub: 'u1' });
  });

  it('returns null for malformed tokens', () => {
    expect(decodeJwtPayload('')).toBeNull();
    expect(decodeJwtPayload('only.one')).toBeNull();
    expect(decodeJwtPayload('a.b.c.d')).toBeNull();
    expect(decodeJwtPayload('aaa.%%%notbase64%%%.bbb')).toBeNull();
  });

  it('isTruthyJwtClaim accepts boolean/string/number truthy forms', () => {
    expect(isTruthyJwtClaim(true)).toBe(true);
    expect(isTruthyJwtClaim('true')).toBe(true);
    expect(isTruthyJwtClaim(1)).toBe(true);
    expect(isTruthyJwtClaim('1')).toBe(true);
    expect(isTruthyJwtClaim(false)).toBe(false);
    expect(isTruthyJwtClaim('false')).toBe(false);
    expect(isTruthyJwtClaim(0)).toBe(false);
    expect(isTruthyJwtClaim(null)).toBe(false);
  });
});
