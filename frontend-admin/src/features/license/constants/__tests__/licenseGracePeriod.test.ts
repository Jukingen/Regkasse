import { describe, expect, it } from 'vitest';

import {
  TENANT_GRACE_PERIOD_DAYS,
  clampTenantGraceRemaining,
  resolveTenantGraceDays,
} from '../licenseGracePeriod';

describe('licenseGracePeriod helpers', () => {
  const nowMs = new Date('2026-07-20T12:00:00Z').getTime();

  it('clamps grace remaining to the 7-day window', () => {
    expect(clampTenantGraceRemaining(997)).toBe(TENANT_GRACE_PERIOD_DAYS);
    expect(clampTenantGraceRemaining(5)).toBe(5);
    expect(clampTenantGraceRemaining(-1)).toBe(0);
  });

  it('prefers gracePeriodRemaining over ValidUntil horizon', () => {
    const result = resolveTenantGraceDays({
      daysRemaining: 997,
      gracePeriodRemaining: 5,
      validUntilUtc: '2029-04-13T00:00:00Z',
      nowMs,
    });

    expect(result.graceRemaining).toBe(5);
    expect(result.daysExpired).toBe(2);
  });

  it('never treats a future ValidUntil as grace remaining', () => {
    const result = resolveTenantGraceDays({
      daysRemaining: 997,
      validUntilUtc: '2029-04-13T00:00:00Z',
      nowMs,
    });

    expect(result.graceRemaining).toBe(0);
    expect(result.daysExpired).toBe(0);
  });

  it('derives grace from past ValidUntil only', () => {
    const result = resolveTenantGraceDays({
      validUntilUtc: '2026-07-18T00:00:00Z',
      nowMs,
    });

    expect(result.daysExpired).toBe(2);
    expect(result.graceRemaining).toBe(5);
  });
});
