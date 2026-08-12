import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  getMandantLicenseBadgeDisplay,
  getTenantSwitcherLicenseBadgeDisplay,
} from '@/features/tenant/utils/mandantLicenseBadge';
import {
  graceUrgentDismissStorageKey,
  isGracePeriodBannerUrgent,
  isGraceUrgentDismissed,
  setGraceUrgentDismissed,
  shouldShowGraceUrgentWarning,
} from '@/features/license/utils/gracePeriodUrgentWarning';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';

const t = (key: string, params?: Record<string, string | number>) =>
  params ? `${key}:${JSON.stringify(params)}` : key;

describe('mandantLicenseBadge', () => {
  it('returns null for none license', () => {
    expect(getMandantLicenseBadgeDisplay(null, null, t)).toBeNull();
  });

  it('builds switcher badges for none/expired/warning/success', () => {
    expect(getTenantSwitcherLicenseBadgeDisplay(null, null, t).color).toBe('default');

    const expiredUntil = new Date(Date.now() - 2 * 86400000).toISOString();
    expect(getTenantSwitcherLicenseBadgeDisplay(expiredUntil, 'REGK', t).color).toBe('error');

    const soon = new Date(Date.now() + 3 * 86400000).toISOString();
    expect(getTenantSwitcherLicenseBadgeDisplay(soon, 'REGK', t).color).toBe('warning');

    const far = new Date(Date.now() + 60 * 86400000).toISOString();
    expect(getTenantSwitcherLicenseBadgeDisplay(far, 'REGK', t).color).toBe('success');
  });

  it('maps trial/valid badges via getMandantLicenseBadgeDisplay', () => {
    const trialUntil = new Date(Date.now() + 20 * 86400000).toISOString();
    const badge = getMandantLicenseBadgeDisplay(trialUntil, 'TRIAL-KEY', t);
    expect(badge).not.toBeNull();
    expect(badge?.daysRemaining).not.toBeNull();
  });
});

describe('gracePeriodUrgentWarning', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  it('gates urgent warning/banner by grace days', () => {
    expect(shouldShowGraceUrgentWarning(null)).toBe(false);
    expect(shouldShowGraceUrgentWarning({ state: 'Active', graceDaysRemaining: 0 })).toBe(false);
    expect(shouldShowGraceUrgentWarning({ state: 'Grace', graceDaysRemaining: 1 })).toBe(true);
    expect(isGracePeriodBannerUrgent({ state: 'Grace', graceDaysRemaining: 2 })).toBe(true);
    expect(isGracePeriodBannerUrgent({ state: 'Grace', graceDaysRemaining: 3 })).toBe(false);
  });

  it('persists dismiss flag in sessionStorage', () => {
    const key = graceUrgentDismissStorageKey('t1', '2026-08-20');
    expect(key).toContain('t1');
    expect(isGraceUrgentDismissed(key)).toBe(false);
    setGraceUrgentDismissed(key);
    expect(isGraceUrgentDismissed(key)).toBe(true);
  });
});

describe('invalidateTenantLicenseQueries', () => {
  it('invalidates and refetches license-related query keys', async () => {
    const invalidateQueries = vi.fn().mockResolvedValue(undefined);
    const refetchQueries = vi.fn().mockResolvedValue(undefined);
    const queryClient = { invalidateQueries, refetchQueries } as never;

    await invalidateTenantLicenseQueries(queryClient, 'tenant-1');
    expect(invalidateQueries.mock.calls.length).toBeGreaterThan(5);
    expect(refetchQueries.mock.calls.length).toBeGreaterThan(2);

    await invalidateTenantLicenseQueries(queryClient, null);
    expect(invalidateQueries).toHaveBeenCalled();
  });
});
