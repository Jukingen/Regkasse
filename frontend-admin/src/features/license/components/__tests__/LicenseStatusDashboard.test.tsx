import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { LicenseStatusView } from '@/hooks/useLicenseStatus';

import {
  ACTIVE_UNTIL_DISPLAY,
  EXTENDED_UNTIL_DISPLAY,
  activeLicenseView,
  expiredLicenseView,
  extendedLicenseView,
  graceLicenseView,
  interpolateT,
} from './licenseUiTestFixtures';

const licenseView = vi.hoisted(() => ({
  current: null as LicenseStatusView | null,
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/license/dashboard',
}));

vi.mock('@/components/admin-layout/AdminPageHeader', () => ({
  AdminPageHeader: ({
    title,
    extra,
  }: {
    title: ReactNode;
    extra?: ReactNode;
  }) => (
    <div>
      <h1>{title}</h1>
      <div>{extra}</div>
    </div>
  ),
}));

vi.mock('@/features/data-management/components/LockedLicenseDataRightsCard', () => ({
  LockedLicenseDataRightsCard: () => <div>data-rights</div>,
}));

vi.mock('@/features/license/hooks/useLicensePageRefresh', () => ({
  useLicensePageRefresh: () => ({ refresh: vi.fn(), isFetching: false }),
}));

vi.mock('@/features/license/hooks/useLicenseRenewalFunnelPageView', () => ({
  useLicenseRenewalFunnelPageView: vi.fn(),
}));

vi.mock('@/features/license/stores/licenseRenewalModalStore', () => ({
  openLicenseRenewalModal: vi.fn(),
}));

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
  useCurrentTenant: () => ({
    tenantId: 'tenant-1',
    isRealTenantSlug: true,
    isSuperAdminUser: false,
    isSuperAdminPlatformMode: false,
    suppressLicenseWarnings: false,
  }),
}));

vi.mock('@/hooks/useLicenseStatus', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/hooks/useLicenseStatus')>();
  return {
    ...actual,
    useLicenseStatus: () => ({
      status: licenseView.current,
      history: [],
      isLoading: false,
      isFetching: false,
    }),
  };
});

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: interpolateT({
      'license.statusDashboard.title': 'Lizenz-Dashboard',
      'license.statusDashboard.subtitle': 'Status, Restlaufzeit und Verlauf Ihrer Mandantenlizenz.',
      'license.statusDashboard.status': 'Status',
      'license.statusDashboard.stateActive': 'Aktiv',
      'license.statusDashboard.stateGrace': 'Grace',
      'license.statusDashboard.stateLocked': 'Gesperrt',
      'license.statusDashboard.daysUntilExpiry': 'Tage bis Ablauf',
      'license.statusDashboard.graceDaysRemaining': 'Grace-Tage übrig',
      'license.statusDashboard.daysOverdue': 'Tage überfällig',
      'license.statusDashboard.validUntil': 'Gültig bis',
      'license.statusDashboard.licensePlan': 'Lizenztyp',
      'license.statusDashboard.planFallback': 'Standard',
      'license.statusDashboard.progressActive': '{{days}} Tage',
      'license.statusDashboard.progressGrace': '{{days}} Grace-Tage',
      'license.statusDashboard.progressExpired': 'Abgelaufen',
      'license.statusDashboard.timelineTitle': 'Lizenz-Verlauf',
      'license.statusDashboard.renew': 'Lizenz verlängern',
      'license.statusDashboard.renewLocked': 'Lizenz jetzt verlängern',
      'license.statusDashboard.lockedAlertTitle': 'System gesperrt',
      'license.statusDashboard.lockedAlertDescription':
        'Ihre Lizenz ist abgelaufen. Bitte verlängern Sie Ihre Lizenz, um alle Funktionen wieder nutzen zu können.',
      'license.statusDashboard.graceAlertTitle': 'Grace-Periode aktiv',
      'license.statusDashboard.graceAlertDescription':
        'Ihre Lizenz ist abgelaufen. Noch {{days}} Tag(e) Grace — bitte zeitnah verlängern.',
      'license.history.empty': 'Kein Verlauf',
      'common.buttons.refresh': 'Aktualisieren',
      'nav.licenseManagement': 'Lizenzverwaltung',
    }),
  }),
}));

import LicenseStatusDashboard from '@/features/license/components/LicenseStatusDashboard';

describe('LicenseStatusDashboard', () => {
  beforeEach(() => {
    licenseView.current = null;
  });

  it('shows Aktiv and days remaining for an active license', () => {
    licenseView.current = activeLicenseView();
    render(<LicenseStatusDashboard />);

    expect(screen.getByText('Aktiv')).toBeInTheDocument();
    expect(screen.getByText('Tage bis Ablauf')).toBeInTheDocument();
    expect(screen.getByText('45')).toBeInTheDocument();
    expect(screen.getByText('45 Tage')).toBeInTheDocument();
    expect(screen.getByText(ACTIVE_UNTIL_DISPLAY)).toBeInTheDocument();
    expect(screen.queryByText('System gesperrt')).not.toBeInTheDocument();
  });

  it('shows Gesperrt, overdue days, and lock message when expired', () => {
    licenseView.current = expiredLicenseView();
    render(<LicenseStatusDashboard />);

    expect(screen.getByText('Gesperrt')).toBeInTheDocument();
    expect(screen.getByText('Tage überfällig')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Abgelaufen')).toBeInTheDocument();
    expect(screen.getByText('System gesperrt')).toBeInTheDocument();
  });

  it('shows a grace warning with remaining days', () => {
    licenseView.current = graceLicenseView();
    render(<LicenseStatusDashboard />);

    expect(screen.getAllByText('Grace').length).toBeGreaterThan(0);
    expect(screen.getByText('Grace-Periode aktiv')).toBeInTheDocument();
    expect(
      screen.getByText(/Noch 5 Tag\(e\) Grace/)
    ).toBeInTheDocument();
    expect(screen.queryByText('System gesperrt')).not.toBeInTheDocument();
  });

  it('after extension shows the new date and removes the lock message', () => {
    licenseView.current = expiredLicenseView();
    const { rerender } = render(<LicenseStatusDashboard />);
    expect(screen.getByText('System gesperrt')).toBeInTheDocument();

    licenseView.current = extendedLicenseView();
    rerender(<LicenseStatusDashboard />);

    expect(screen.queryByText('System gesperrt')).not.toBeInTheDocument();
    expect(screen.getByText('Aktiv')).toBeInTheDocument();
    expect(screen.getByText(EXTENDED_UNTIL_DISPLAY)).toBeInTheDocument();
  });
});
