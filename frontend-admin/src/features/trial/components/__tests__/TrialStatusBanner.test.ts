import { describe, expect, it } from 'vitest';

import { resolveTrialBannerVariant, type TrialBannerTenant } from '../TrialStatusBanner';

describe('resolveTrialBannerVariant', () => {
  it('hides banner for converted and deleted trials', () => {
    expect(resolveTrialBannerVariant({ trialStatus: 'converted', trialDaysRemaining: 3 })).toBeNull();
    expect(resolveTrialBannerVariant({ trialStatus: 'deleted', trialDaysRemaining: 1 })).toBeNull();
    expect(resolveTrialBannerVariant({ trialStatus: null, trialDaysRemaining: 5 })).toBeNull();
  });

  it('uses info when more than 7 days remain', () => {
    expect(
      resolveTrialBannerVariant({ trialStatus: 'active', trialDaysRemaining: 14 })
    ).toBe('info');
  });

  it('uses warning in the last 7 days', () => {
    expect(
      resolveTrialBannerVariant({ trialStatus: 'active', trialDaysRemaining: 3 })
    ).toBe('warning');
  });

  it('treats overdue active trials as expiredActive', () => {
    expect(
      resolveTrialBannerVariant({ trialStatus: 'active', trialDaysRemaining: 0 })
    ).toBe('expiredActive');
  });

  it('uses expired status when backend already marked expired', () => {
    expect(
      resolveTrialBannerVariant({ trialStatus: 'expired', trialDaysRemaining: -2 })
    ).toBe('expired');
  });

  it('derives remaining days from trialEndsAtUtc when trialDaysRemaining is missing', () => {
    const tenant: TrialBannerTenant = {
      trialStatus: 'active',
      trialEndsAtUtc: new Date(Date.now() + 10 * 24 * 60 * 60 * 1000).toISOString(),
    };
    expect(resolveTrialBannerVariant(tenant)).toBe('info');
  });
});
