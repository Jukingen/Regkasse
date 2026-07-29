import { describe, expect, it } from '@jest/globals';

import {
  licenseReminderNotificationId,
  planLicenseReminderFireTimes,
} from '../utils/licenseReminderSchedule';

describe('planLicenseReminderFireTimes', () => {
  const nowMs = Date.parse('2026-07-20T12:00:00.000Z');
  const expiry = new Date('2026-08-10T12:00:00.000Z'); // 21 days ahead

  it('schedules only future milestones before expiry', () => {
    const plans = planLicenseReminderFireTimes(expiry, nowMs);
    expect(plans.map((p) => p.days)).toEqual([14, 7, 1]);
    expect(plans[0]?.fireAt.toISOString()).toBe('2026-07-27T12:00:00.000Z');
    expect(plans[1]?.fireAt.toISOString()).toBe('2026-08-03T12:00:00.000Z');
    expect(plans[2]?.fireAt.toISOString()).toBe('2026-08-09T12:00:00.000Z');
  });

  it('returns empty when expiry is in the past', () => {
    expect(planLicenseReminderFireTimes(new Date('2026-07-01T00:00:00.000Z'), nowMs)).toEqual([]);
  });

  it('builds stable notification identifiers', () => {
    expect(licenseReminderNotificationId(7)).toBe('license-expiry-7d');
  });
});
