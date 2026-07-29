import { describe, expect, it } from 'vitest';

import type { LicenseStatusView } from '@/hooks/useLicenseStatus';

import {
  getRenewalModalStatusSummary,
  renewalModalIconColor,
} from '../renewalModalStatusSummary';

function baseStatus(overrides: Partial<LicenseStatusView>): LicenseStatusView {
  return {
    state: 'Active',
    graceDaysRemaining: 0,
    daysOverdue: 0,
    daysUntilExpiry: 0,
    licensePlan: '12_months',
    expiredAt: '2026-08-04T00:00:00.000Z',
    graceEndedAt: null,
    canWrite: true,
    kind: 'active',
    ...overrides,
  };
}

describe('getRenewalModalStatusSummary', () => {
  it('does not present Active licenses as locked/expired', () => {
    const summary = getRenewalModalStatusSummary(
      baseStatus({ state: 'Active', daysUntilExpiry: 8, daysOverdue: 0 })
    );

    expect(summary.state).toBe('Active');
    expect(summary.tone).toBe('success');
    expect(summary.statusValueKey).toBe('license.renewalModal.statusActive');
    expect(summary.dateLabelKey).toBe('license.renewalModal.validUntilLabel');
    expect(summary.daysLabelKey).toBe('license.renewalModal.daysRemainingLabel');
    expect(summary.daysValue).toBe(8);
    expect(summary.daysDanger).toBe(false);
    expect(summary.headingKey).toBe('license.renewalModal.headingActive');
  });

  it('shows grace remaining during Grace', () => {
    const summary = getRenewalModalStatusSummary(
      baseStatus({
        state: 'Grace',
        daysOverdue: 2,
        graceDaysRemaining: 5,
        kind: 'grace_write',
        canWrite: true,
      })
    );

    expect(summary.state).toBe('Grace');
    expect(summary.tone).toBe('warning');
    expect(summary.statusValueKey).toBe('license.renewalModal.statusGrace');
    expect(summary.dateLabelKey).toBe('license.renewalModal.expiredAtLabel');
    expect(summary.daysValue).toBe(5);
    expect(summary.daysDanger).toBe(false);
  });

  it('shows overdue days when Locked', () => {
    const summary = getRenewalModalStatusSummary(
      baseStatus({
        state: 'Locked',
        daysOverdue: 12,
        daysUntilExpiry: 0,
        kind: 'lockdown',
        canWrite: false,
      })
    );

    expect(summary.state).toBe('Locked');
    expect(summary.tone).toBe('danger');
    expect(summary.statusValueKey).toBe('license.renewalModal.statusLocked');
    expect(summary.daysValue).toBe(12);
    expect(summary.daysDanger).toBe(true);
    expect(renewalModalIconColor(summary.tone)).toBe('#ff4d4f');
  });
});
