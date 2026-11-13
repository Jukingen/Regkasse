import { describe, expect, it } from 'vitest';

import {
  NIL_REGISTER_UUID,
  analyzeRegisterFkField,
  buildFinanzOnlineQueuePath,
  parseAuthoritativePaymentGuid,
  parseAuthoritativeRegisterGuid,
  toLinkSafeRegisterRowId,
} from '@/shared/utils/registerIdentity';

describe('analyzeRegisterFkField', () => {
  it('returns empty analysis for blank input', () => {
    expect(analyzeRegisterFkField('  ')).toEqual({
      rawTrimmed: undefined,
      linkSafeUuid: undefined,
      isRawPresentButNotLinkSafe: false,
    });
  });

  it('keeps raw non-UUID visible and flags not link-safe', () => {
    const a = analyzeRegisterFkField(' KASSE-01 ');
    expect(a.rawTrimmed).toBe('KASSE-01');
    expect(a.linkSafeUuid).toBeUndefined();
    expect(a.isRawPresentButNotLinkSafe).toBe(true);
  });

  it('treats nil UUID as present but not link-safe', () => {
    const a = analyzeRegisterFkField(NIL_REGISTER_UUID);
    expect(a.rawTrimmed).toBe(NIL_REGISTER_UUID);
    expect(a.linkSafeUuid).toBeUndefined();
    expect(a.isRawPresentButNotLinkSafe).toBe(true);
  });

  it('accepts RFC-like variant UUID for links', () => {
    const id = '11111111-1111-4111-8111-111111111111';
    const a = analyzeRegisterFkField(id);
    expect(a.rawTrimmed).toBe(id);
    expect(a.linkSafeUuid).toBe(id);
    expect(a.isRawPresentButNotLinkSafe).toBe(false);
  });
});

describe('toLinkSafeRegisterRowId', () => {
  it('matches parseAuthoritativeRegisterGuid (never treats display text as link input)', () => {
    expect(toLinkSafeRegisterRowId('KASSE-1')).toBeUndefined();
    expect(toLinkSafeRegisterRowId(undefined)).toBeUndefined();
    const id = '11111111-1111-4111-8111-111111111111';
    expect(toLinkSafeRegisterRowId(id)).toBe(id);
    expect(toLinkSafeRegisterRowId(id)).toBe(parseAuthoritativeRegisterGuid(id));
  });
});

describe('buildFinanzOnlineQueuePath', () => {
  it('omits cashRegisterId query when value is not link-safe', () => {
    const path = buildFinanzOnlineQueuePath({ registerRowId: 'display-only-42' });
    expect(path).not.toContain('cashRegisterId=display');
    expect(path).toContain('/rksv/finanz-online-queue');
  });

  it('includes cashRegisterId only for parsed UUID', () => {
    const id = '22222222-2222-4222-8222-222222222222';
    const path = buildFinanzOnlineQueuePath({ registerRowId: id });
    expect(path).toContain(`cashRegisterId=${encodeURIComponent(id)}`);
  });
});

describe('parseAuthoritativeRegisterGuid', () => {
  it('rejects UUID with wrong variant nibble', () => {
    expect(parseAuthoritativeRegisterGuid('11111111-1111-4111-c111-111111111111')).toBeUndefined();
  });
});

describe('parseAuthoritativePaymentGuid', () => {
  it('matches register UUID rule (payment focus ids use the same strict check)', () => {
    const id = '33333333-3333-4333-8333-333333333333';
    expect(parseAuthoritativePaymentGuid(id)).toBe(id);
    expect(parseAuthoritativePaymentGuid(id)).toBe(parseAuthoritativeRegisterGuid(id));
    expect(parseAuthoritativePaymentGuid('receipt-123')).toBeUndefined();
  });
});
