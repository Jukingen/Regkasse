import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { LicenseLockdownBanner } from '@/components/LicenseLockdownBanner';
import type { LicenseStatusView } from '@/hooks/useLicenseStatus';

const pushMock = vi.fn();
const openRenewalMock = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock('@/features/license/stores/licenseRenewalModalStore', () => ({
  openLicenseRenewalModal: (...args: unknown[]) => openRenewalMock(...args),
}));

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
  useCurrentTenant: () => ({
    tenantId: 'tenant-1',
    isRealTenantSlug: true,
    suppressLicenseWarnings: false,
    isSuperAdminUser: false,
  }),
}));

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizationGate: () => ({ isAuthorized: true }),
}));

vi.mock('@/hooks/useLicenseStatus', () => ({
  useLicenseStatus: () => ({
    status: null,
    isLoading: false,
  }),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, string | number>) => {
      const de: Record<string, string> = {
        'license.statusBanner.locked.title': 'Eingeschränkter Modus',
        'license.statusBanner.archived.title': 'Eingeschränkter Modus — Archiviert',
        'license.statusBanner.locked.description':
          `Ihre Lizenz ist seit ${params?.days ?? ''} Tag(en) abgelaufen.`,
        'license.statusBanner.locked.bulletReadOnly':
          'Sie können nur lesend auf Ihre Daten zugreifen',
        'license.statusBanner.locked.bulletNoWrite':
          'Keine Änderungen oder Neuanlagen möglich',
        'license.statusBanner.locked.bulletRenew': 'Bitte verlängern Sie Ihre Lizenz',
        'license.statusBanner.actions.renew': 'Lizenz jetzt verlängern',
        'license.statusBanner.actions.openLicensePage': 'Zur Lizenzseite',
        'license.statusBanner.actions.dataExport': 'Datenexport anfordern',
        'license.statusBanner.actions.accountManagement': 'Kontoverwaltung',
      };
      return de[key] ?? key;
    },
  }),
}));

function lockedStatus(overrides?: Partial<LicenseStatusView>): LicenseStatusView {
  return {
    state: 'Locked',
    graceDaysRemaining: 0,
    daysOverdue: 12,
    daysUntilExpiry: 0,
    licensePlan: 'Standard',
    expiredAt: '2026-07-01T00:00:00Z',
    graceEndedAt: '2026-07-08T00:00:00Z',
    canWrite: false,
    kind: 'lockdown',
    anyActive: false,
    allActive: false,
    ...overrides,
  };
}

describe('LicenseLockdownBanner', () => {
  beforeEach(() => {
    pushMock.mockClear();
    openRenewalMock.mockClear();
  });

  it('renders nothing when license is active', () => {
    const { container } = render(
      <LicenseLockdownBanner status={lockedStatus({ state: 'Active', kind: 'active' })} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('shows restricted-mode copy and renew CTA when locked', () => {
    render(<LicenseLockdownBanner status={lockedStatus()} />);
    expect(screen.getByRole('alert')).toBeTruthy();
    expect(screen.getByText('Eingeschränkter Modus')).toBeTruthy();
    expect(screen.getByText(/12 Tag/)).toBeTruthy();
    expect(screen.getByRole('button', { name: /Lizenz jetzt verlängern/i })).toBeTruthy();
  });

  it('opens renewal modal from primary CTA', async () => {
    const user = userEvent.setup();
    render(<LicenseLockdownBanner status={lockedStatus()} />);
    await user.click(screen.getByRole('button', { name: /Lizenz jetzt verlängern/i }));
    expect(openRenewalMock).toHaveBeenCalled();
  });
});
