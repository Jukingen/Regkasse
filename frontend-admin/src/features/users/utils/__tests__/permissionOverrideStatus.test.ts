import { describe, expect, it } from 'vitest';

import { computePermissionOverrideStatus } from '../permissionOverrideStatus';

describe('computePermissionOverrideStatus', () => {
  const now = new Date('2026-07-22T12:00:00.000Z');

  it('returns expired when expiresAt is in the past', () => {
    expect(
      computePermissionOverrideStatus(null, '2026-07-22T11:59:59.000Z', now)
    ).toBe('expired');
  });

  it('returns scheduled when validFrom is in the future', () => {
    expect(
      computePermissionOverrideStatus('2026-07-23T00:00:00.000Z', '2026-08-01T00:00:00.000Z', now)
    ).toBe('scheduled');
  });

  it('returns expiringSoon when expires within window', () => {
    expect(
      computePermissionOverrideStatus(null, '2026-07-23T12:00:00.000Z', now, 48)
    ).toBe('expiringSoon');
  });

  it('returns active when within validity and not near expiry', () => {
    expect(
      computePermissionOverrideStatus(
        '2026-07-01T00:00:00.000Z',
        '2026-08-01T00:00:00.000Z',
        now,
        48
      )
    ).toBe('active');
  });

  it('returns active when no dates', () => {
    expect(computePermissionOverrideStatus(null, null, now)).toBe('active');
  });
});
