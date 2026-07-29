import { describe, expect, it } from 'vitest';

import { mapLicenseActivityFeedItem } from '@/features/license/utils/licenseActivityFeed';

describe('mapLicenseActivityFeedItem', () => {
  it('maps activate / ACTIVATED to renewal', () => {
    expect(
      mapLicenseActivityFeedItem({
        action: 'activate',
        sourceCode: 'ACTIVATED',
        licenseKeyMasked: 'REGK-****',
        timestampUtc: '2026-07-20T12:00:00.000Z',
      })
    ).toMatchObject({
      type: 'renewal',
      descriptionKey: 'license.activityLog.descriptions.activated',
      descriptionParams: { key: 'REGK-****' },
    });

    expect(
      mapLicenseActivityFeedItem({
        action: 'ACTIVATED',
        timestampUtc: '2026-07-20T12:00:00.000Z',
      }).type
    ).toBe('renewal');
  });

  it('maps reminder tokens', () => {
    expect(
      mapLicenseActivityFeedItem({
        action: 'LICENSE_REMINDER_SENT',
        timestampUtc: '2026-07-20T12:00:00.000Z',
      })
    ).toMatchObject({
      type: 'reminder',
      descriptionKey: 'license.activityLog.descriptions.reminder',
    });
  });

  it('maps revoke / cancel to expiry', () => {
    expect(
      mapLicenseActivityFeedItem({
        action: 'revoke',
        timestampUtc: '2026-07-20T12:00:00.000Z',
      }).type
    ).toBe('expiry');
    expect(
      mapLicenseActivityFeedItem({
        action: 'cancel',
        timestampUtc: '2026-07-20T12:00:00.000Z',
      }).descriptionKey
    ).toBe('license.activityLog.descriptions.cancelled');
  });

  it('maps details to view', () => {
    expect(
      mapLicenseActivityFeedItem({
        action: 'details',
        timestampUtc: '2026-07-20T12:00:00.000Z',
      })
    ).toMatchObject({
      type: 'view',
      descriptionKey: 'license.activityLog.descriptions.viewed',
    });
  });

  it('falls back to other for unknown actions', () => {
    expect(
      mapLicenseActivityFeedItem({
        action: 'mystery',
        timestampUtc: '2026-07-20T12:00:00.000Z',
      }).type
    ).toBe('other');
  });
});
