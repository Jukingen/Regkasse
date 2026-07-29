import { describe, expect, it, afterEach } from 'vitest';

import {
  getLicenseRenewalChecklistProgressPercent,
  loadLicenseRenewalChecklistCompleted,
  saveLicenseRenewalChecklistCompleted,
  toggleLicenseRenewalChecklistItem,
} from '../licenseRenewalChecklist';

describe('licenseRenewalChecklist', () => {
  afterEach(() => {
    localStorage.clear();
  });

  it('toggles items and persists per tenant', () => {
    let set = new Set(loadLicenseRenewalChecklistCompleted('t1'));
    expect(set.size).toBe(0);

    set = toggleLicenseRenewalChecklistItem(set, 'prepareLicenseKey');
    saveLicenseRenewalChecklistCompleted('t1', set);

    const loaded = loadLicenseRenewalChecklistCompleted('t1');
    expect(loaded.has('prepareLicenseKey')).toBe(true);
    expect(loadLicenseRenewalChecklistCompleted('t2').size).toBe(0);
  });

  it('computes progress percent safely', () => {
    expect(getLicenseRenewalChecklistProgressPercent(0, 0)).toBe(0);
    expect(getLicenseRenewalChecklistProgressPercent(2, 5)).toBe(40);
    expect(getLicenseRenewalChecklistProgressPercent(5, 5)).toBe(100);
  });
});
