import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { LicenseExpiryBanner } from '@/components/admin-layout/LicenseExpiryBanner';
import type { LicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import type { LicenseStatusView } from '@/hooks/useLicenseStatus';

import {
  activeLicenseView,
  expiredLicenseView,
  extendedLicenseView,
  graceLicenseView,
  interpolateT,
  tenantLicenseStatus,
} from '@/features/license/components/__tests__/licenseUiTestFixtures';

const tenantLicense = vi.hoisted(() => ({
  current: undefined as LicenseStatus | undefined,
}));

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

vi.mock('@/features/license/hooks/useLicenseStatus', () => ({
  useTenantLicenseStatus: () => ({ data: tenantLicense.current }),
  useDeploymentLicenseStatus: () => ({ data: undefined }),
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
      'license.statusBanner.locked.title': 'Eingeschränkter Modus',
      'license.statusBanner.archived.title': 'Eingeschränkter Modus — Archiviert',
      'license.statusBanner.locked.description':
        'Ihre Lizenz ist seit {{days}} Tag(en) abgelaufen.',
      'license.statusBanner.locked.bulletReadOnly':
        'Sie können nur lesend auf Ihre Daten zugreifen',
      'license.statusBanner.locked.bulletNoWrite': 'Keine Änderungen oder Neuanlagen möglich',
      'license.statusBanner.locked.bulletRenew': 'Bitte verlängern Sie Ihre Lizenz',
      'license.statusBanner.actions.renew': 'Lizenz jetzt verlängern',
      'license.statusBanner.actions.openLicensePage': 'Zur Lizenzseite',
      'license.statusBanner.actions.dataExport': 'Datenexport anfordern',
      'license.statusBanner.actions.accountManagement': 'Kontoverwaltung',
      'license.gracePeriodBanner.title': 'Lizenz läuft in {{days}} Tag(en) ab',
      'license.gracePeriodBanner.titleUrgent': 'DRINGEND: Lizenz läuft in {{days}} Tag(en) ab',
      'license.gracePeriodBanner.remainingLabel':
        'Grace-Periode: {{days}} von {{total}} Tagen verbleibend',
      'license.gracePeriodBanner.lockdownHint':
        'Nach Ablauf der Grace-Periode wird das System gesperrt. Sie können dann nur noch lesend auf Ihre Daten zugreifen.',
      'license.gracePeriodBanner.progressAria':
        'Grace-Periode verbraucht: {{days}} Tag(e) von {{total}} verbleibend',
      'license.gracePeriodBanner.renew': 'Verlängern',
      'license.gracePeriodBanner.renewUrgent': 'Jetzt verlängern',
    }),
  }),
}));

describe('LicenseExpiryBanner', () => {
  beforeEach(() => {
    tenantLicense.current = undefined;
    licenseView.current = null;
  });

  it('renders nothing when the mandant license is active', () => {
    tenantLicense.current = tenantLicenseStatus('active');
    licenseView.current = activeLicenseView();
    const { container } = render(<LicenseExpiryBanner />);
    expect(container).toBeEmptyDOMElement();
  });

  it('shows a grace warning with remaining days', () => {
    tenantLicense.current = tenantLicenseStatus('grace_write');
    licenseView.current = graceLicenseView();
    render(<LicenseExpiryBanner />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Lizenz läuft in 5 Tag(en) ab')).toBeInTheDocument();
  });

  it('shows an error banner with overdue days when locked', () => {
    tenantLicense.current = tenantLicenseStatus('lockdown');
    licenseView.current = expiredLicenseView();
    render(<LicenseExpiryBanner />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Eingeschränkter Modus')).toBeInTheDocument();
    expect(screen.getByText(/12 Tag/)).toBeInTheDocument();
  });

  it('hides the lock banner after extension returns the license to active', () => {
    tenantLicense.current = tenantLicenseStatus('lockdown');
    licenseView.current = expiredLicenseView();
    const { rerender } = render(<LicenseExpiryBanner />);
    expect(screen.getByText('Eingeschränkter Modus')).toBeInTheDocument();

    tenantLicense.current = tenantLicenseStatus('active', {
      daysRemaining: 504,
      daysExpired: 0,
      isExpired: false,
      isLocked: false,
      canAccess: true,
      canWrite: true,
    });
    licenseView.current = extendedLicenseView();
    rerender(<LicenseExpiryBanner />);

    expect(screen.queryByText('Eingeschränkter Modus')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
