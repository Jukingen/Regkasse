import { render, screen } from '@testing-library/react';
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
} from '@/features/license/components/__tests__/licenseUiTestFixtures';

const licenseView = vi.hoisted(() => ({
  current: null as LicenseStatusView | null,
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
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

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizationGate: () => ({ isAuthorized: true }),
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
      'dashboard.widgets.licenseStatus.title': 'Lizenz-Status',
      'dashboard.widgets.licenseStatus.status': 'Status',
      'dashboard.widgets.licenseStatus.validUntil': 'Gültig bis',
      'dashboard.widgets.licenseStatus.expired': 'Abgelaufen',
      'dashboard.widgets.licenseStatus.remainingHint': 'verbleibende Zeit',
      'dashboard.widgets.licenseStatus.healthy': 'In Ordnung',
      'dashboard.widgets.licenseStatus.actionNeeded': 'Handlungsbedarf',
      'dashboard.widgets.licenseStatus.daysLeft': '{{days}} Tage',
      'dashboard.widgets.licenseStatus.extend': 'Verlängern',
      'dashboard.widgets.licenseStatus.extendNow': 'Jetzt verlängern',
      'dashboard.widgets.licenseStatus.progressAria': 'Lizenz-Status: {{days}} Tag(e)',
      'dashboard.widgets.licenseHealth.states.Active': 'Aktiv',
      'dashboard.widgets.licenseHealth.states.Grace': 'Grace-Periode',
      'dashboard.widgets.licenseHealth.states.Locked': 'Gesperrt',
      'dashboard.widgets.licenseHealth.states.Archived': 'Archiviert',
      'dashboard.widgets.licenseImpact.current.daysValid': '{{days}} Tage gültig',
      'dashboard.widgets.licenseImpact.current.daysGrace': 'Noch {{days}} Tage Grace',
      'dashboard.widgets.licenseImpact.current.daysOverdue': '{{days}} Tage überfällig',
      'dashboard.widgets.licenseImpact.alert.okTitle': 'Alles in Ordnung',
      'dashboard.widgets.licenseImpact.alert.okDescription':
        'Ihre Lizenz ist noch gültig. Bitte denken Sie an die rechtzeitige Verlängerung.',
      'dashboard.widgets.licenseImpact.alert.actionTitle': 'Handlungsbedarf',
      'dashboard.widgets.licenseImpact.alert.actionDescription':
        'Ihre Lizenz läuft ab oder ist abgelaufen. Bitte jetzt verlängern.',
      'license.gracePeriodWidget.title': 'Grace-Periode aktiv',
      'license.gracePeriodWidget.description':
        'Sie haben noch {{days}} Tag(e) Zeit, um Ihre Lizenz zu verlängern. Danach wird das System gesperrt.',
      'license.gracePeriodWidget.daysRemaining': 'Tage verbleibend',
      'license.gracePeriodWidget.labelExpired': 'Lizenz abgelaufen',
      'license.gracePeriodWidget.labelGrace': 'Grace-Periode',
      'license.gracePeriodWidget.labelLockdown': 'Sperrung',
      'license.gracePeriodWidget.progressAria':
        'Grace-Periode: {{days}} Tag(e) von {{total}} verbleibend',
    }),
  }),
}));

import { ManagerLicenseStatusCard } from '@/features/dashboard/components/ManagerLicenseStatusCard';

describe('ManagerLicenseStatusCard', () => {
  beforeEach(() => {
    licenseView.current = null;
  });

  it('shows Aktiv and remaining days for an active license', () => {
    licenseView.current = activeLicenseView();
    render(<ManagerLicenseStatusCard />);

    expect(screen.getByText('Aktiv')).toBeInTheDocument();
    expect(screen.getByText('45 Tage')).toBeInTheDocument();
    expect(screen.getByText(ACTIVE_UNTIL_DISPLAY)).toBeInTheDocument();
    expect(screen.getByText('In Ordnung')).toBeInTheDocument();
  });

  it('shows Abgelaufen and overdue days when the license is locked', () => {
    licenseView.current = expiredLicenseView();
    render(<ManagerLicenseStatusCard />);

    expect(screen.getByText('Gesperrt')).toBeInTheDocument();
    expect(screen.getAllByText('Abgelaufen').length).toBeGreaterThan(0);
    expect(screen.getByText('12 Tage')).toBeInTheDocument();
    expect(screen.getByText(/12 Tage überfällig/)).toBeInTheDocument();
  });

  it('shows a grace warning with days left', () => {
    licenseView.current = graceLicenseView();
    render(<ManagerLicenseStatusCard />);

    expect(screen.getAllByText('Grace-Periode').length).toBeGreaterThan(0);
    expect(screen.getByText('Grace-Periode aktiv')).toBeInTheDocument();
    expect(screen.getByText(/noch 5 Tag\(e\) Zeit/i)).toBeInTheDocument();
  });

  it('after extension shows the new date and no longer shows Abgelaufen as status', () => {
    licenseView.current = expiredLicenseView();
    const { rerender } = render(<ManagerLicenseStatusCard />);
    expect(screen.getAllByText('Abgelaufen').length).toBeGreaterThan(0);

    licenseView.current = extendedLicenseView();
    rerender(<ManagerLicenseStatusCard />);

    expect(screen.getByText('Aktiv')).toBeInTheDocument();
    expect(screen.getByText(EXTENDED_UNTIL_DISPLAY)).toBeInTheDocument();
    expect(screen.queryByText('Gesperrt')).not.toBeInTheDocument();
  });
});
