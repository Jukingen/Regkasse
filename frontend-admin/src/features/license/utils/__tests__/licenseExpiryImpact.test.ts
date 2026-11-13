import { describe, expect, it } from 'vitest';

import {
  getLicenseExpiryImpactModel,
  getLicenseImpactAccentStyles,
} from '../licenseExpiryImpact';

describe('getLicenseExpiryImpactModel', () => {
  it('marks Active as healthy when more than 7 days remain', () => {
    const model = getLicenseExpiryImpactModel({
      state: 'Active',
      daysUntilExpiry: 30,
      graceDaysRemaining: 0,
      daysOverdue: 0,
    });
    expect(model.alertType).toBe('info');
    expect(model.currentAccent).toBe('ok');
    expect(model.currentDaysKind).toBe('untilExpiry');
    expect(model.currentDaysLabelValue).toBe(30);
  });

  it('warns when Active and within 7 days of expiry', () => {
    const model = getLicenseExpiryImpactModel({
      state: 'Active',
      daysUntilExpiry: 5,
      graceDaysRemaining: 0,
      daysOverdue: 0,
    });
    expect(model.alertType).toBe('warning');
    expect(model.currentAccent).toBe('warn');
    expect(model.graceAccent).toBe('warn');
  });

  it('highlights Grace as action-needed', () => {
    const model = getLicenseExpiryImpactModel({
      state: 'Grace',
      daysUntilExpiry: 0,
      graceDaysRemaining: 3,
      daysOverdue: 4,
    });
    expect(model.alertType).toBe('error');
    expect(model.currentDaysKind).toBe('graceRemaining');
    expect(model.currentDaysLabelValue).toBe(3);
  });

  it('marks Locked as full danger', () => {
    const model = getLicenseExpiryImpactModel({
      state: 'Locked',
      daysUntilExpiry: 0,
      graceDaysRemaining: 0,
      daysOverdue: 10,
    });
    expect(model.alertType).toBe('error');
    expect(model.lockedAccent).toBe('danger');
    expect(model.currentDaysKind).toBe('overdue');
  });
});

describe('getLicenseImpactAccentStyles', () => {
  it('returns border/background pairs', () => {
    expect(getLicenseImpactAccentStyles('ok').borderColor).toBe('#b7eb8f');
    expect(getLicenseImpactAccentStyles('danger').background).toBe('#fff1f0');
  });
});
