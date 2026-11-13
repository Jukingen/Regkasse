import { describe, expect, it } from 'vitest';

import {
  isPortalProfileComplete,
  portalLicenseDaysCopy,
  portalLicenseStatusColor,
  portalLicenseStatusLabelKey,
  portalOpenInvoiceCount,
  resolvePortalDisplayName,
} from '@/features/tenant-portal/utils/tenantPortalDisplay';

describe('tenantPortalDisplay', () => {
  it('resolves display name from first and last name', () => {
    expect(resolvePortalDisplayName('Anna', 'Huber', 'ahuber')).toBe('Anna Huber');
    expect(resolvePortalDisplayName(null, null, 'manager1')).toBe('manager1');
    expect(resolvePortalDisplayName(null, null, null)).toBe('Manager');
  });

  it('maps license lifecycle to labels and colors', () => {
    expect(portalLicenseStatusLabelKey('Active')).toBe('tenantPortal.portal.licenseActive');
    expect(portalLicenseStatusLabelKey('Grace')).toBe('tenantPortal.portal.licenseGrace');
    expect(portalLicenseStatusLabelKey('Locked')).toBe('tenantPortal.portal.licenseExpired');
    expect(portalLicenseStatusLabelKey('Archived')).toBe('tenantPortal.portal.licenseExpired');
    expect(portalLicenseStatusColor('Active')).toBe('green');
    expect(portalLicenseStatusColor('Grace')).toBe('orange');
    expect(portalLicenseStatusColor('Locked')).toBe('red');
  });

  it('builds remaining / expired day copy', () => {
    expect(
      portalLicenseDaysCopy({
        state: 'Active',
        daysUntilExpiry: 12,
        graceDaysRemaining: 0,
        daysOverdue: 0,
      })
    ).toEqual({ key: 'tenantPortal.portal.daysRemaining', days: 12 });
    expect(
      portalLicenseDaysCopy({
        state: 'Grace',
        daysUntilExpiry: 0,
        graceDaysRemaining: 4,
        daysOverdue: 3,
      })
    ).toEqual({ key: 'tenantPortal.portal.daysRemaining', days: 4 });
    expect(
      portalLicenseDaysCopy({
        state: 'Locked',
        daysUntilExpiry: 0,
        graceDaysRemaining: 0,
        daysOverdue: 9,
      })
    ).toEqual({ key: 'tenantPortal.portal.expiredDays', days: 9 });
  });

  it('counts open invoices as unpaid leftovers', () => {
    expect(portalOpenInvoiceCount({ totalCount: 5, activeCount: 4, cancelledCount: 1 })).toBe(0);
    expect(portalOpenInvoiceCount({ totalCount: 3, activeCount: 2, cancelledCount: 0 })).toBe(1);
    expect(portalOpenInvoiceCount(null)).toBe(0);
  });

  it('detects complete onboarding', () => {
    expect(isPortalProfileComplete({ isFullyComplete: true, completedCount: 1, totalCount: 4 })).toBe(
      true
    );
    expect(isPortalProfileComplete({ completedCount: 4, totalCount: 4 })).toBe(true);
    expect(isPortalProfileComplete({ completedCount: 1, totalCount: 4 })).toBe(false);
    expect(isPortalProfileComplete(null)).toBe(false);
  });
});
