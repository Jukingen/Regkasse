import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { GracePeriodBanner } from '@/components/GracePeriodBanner';
import { GracePeriodWidget } from '@/components/GracePeriodWidget';
import type { LicenseStatusView } from '@/hooks/useLicenseStatus';

const pushMock = vi.fn();
const openRenewalMock = vi.fn();

let mockStatus: LicenseStatusView | null = null;

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
    isSuperAdminPlatformMode: false,
  }),
}));

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizationGate: () => ({ isAuthorized: true }),
}));

vi.mock('@/hooks/useLicenseStatus', () => ({
  useLicenseStatus: () => ({
    status: mockStatus,
    isLoading: false,
  }),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, string | number>) => {
      const map: Record<string, string> = {
        'license.gracePeriodBanner.title': `Lizenz läuft in ${params?.days ?? ''} Tag(en) ab`,
        'license.gracePeriodBanner.titleUrgent': `DRINGEND: Lizenz läuft in ${params?.days ?? ''} Tag(en) ab`,
        'license.gracePeriodBanner.remainingLabel': `Grace-Periode: ${params?.days ?? ''} von ${params?.total ?? ''} Tagen verbleibend`,
        'license.gracePeriodBanner.lockdownHint':
          'Nach Ablauf der Grace-Periode wird das System gesperrt.',
        'license.gracePeriodBanner.progressAria': 'progress',
        'license.gracePeriodBanner.renew': 'Verlängern',
        'license.gracePeriodBanner.renewUrgent': 'Jetzt verlängern',
        'license.gracePeriodWidget.title': 'Grace-Periode aktiv',
        'license.gracePeriodWidget.description': `Sie haben noch ${params?.days ?? ''} Tag(e) Zeit.`,
        'license.gracePeriodWidget.daysRemaining': 'Tage verbleibend',
        'license.gracePeriodWidget.labelExpired': 'Lizenz abgelaufen',
        'license.gracePeriodWidget.labelGrace': 'Grace-Periode',
        'license.gracePeriodWidget.labelLockdown': 'Sperrung',
        'license.gracePeriodWidget.progressAria': 'progress',
        'license.gracePeriodWidget.renew': 'Lizenz verlängern',
      };
      return map[key] ?? key;
    },
  }),
}));

function graceStatus(overrides?: Partial<LicenseStatusView>): LicenseStatusView {
  return {
    state: 'Grace',
    graceDaysRemaining: 5,
    daysOverdue: 2,
    daysUntilExpiry: 0,
    licensePlan: 'Standard',
    expiredAt: '2026-07-20T00:00:00Z',
    graceEndedAt: '2026-07-27T00:00:00Z',
    canWrite: true,
    kind: 'grace_write',
    ...overrides,
  };
}

describe('GracePeriodBanner', () => {
  beforeEach(() => {
    pushMock.mockClear();
    openRenewalMock.mockClear();
    mockStatus = graceStatus();
  });

  it('renders warning copy when more than 2 grace days remain', () => {
    mockStatus = graceStatus({ graceDaysRemaining: 5 });
    render(<GracePeriodBanner />);
    expect(screen.getByText(/Lizenz läuft in 5 Tag\(en\) ab/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Verlängern' })).toBeInTheDocument();
  });

  it('escalates to urgent styling copy when ≤2 days remain', () => {
    mockStatus = graceStatus({ graceDaysRemaining: 2 });
    render(<GracePeriodBanner />);
    expect(screen.getByText(/DRINGEND: Lizenz läuft in 2 Tag\(en\) ab/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Jetzt verlängern' })).toBeInTheDocument();
  });

  it('opens renewal modal on CTA', async () => {
    const user = userEvent.setup();
    render(<GracePeriodBanner />);
    await user.click(screen.getByRole('button', { name: 'Verlängern' }));
    expect(openRenewalMock).toHaveBeenCalledTimes(1);
  });

  it('hides when not in grace', () => {
    mockStatus = graceStatus({ state: 'Active', graceDaysRemaining: 0, kind: 'active' });
    const { container } = render(<GracePeriodBanner />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('GracePeriodWidget', () => {
  beforeEach(() => {
    openRenewalMock.mockClear();
    mockStatus = graceStatus({ graceDaysRemaining: 3 });
  });

  it('shows remaining days and timeline labels', () => {
    render(<GracePeriodWidget />);
    expect(screen.getByText('Grace-Periode aktiv')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('Lizenz abgelaufen')).toBeInTheDocument();
    expect(screen.getByText('Sperrung')).toBeInTheDocument();
  });

  it('opens renewal modal from widget CTA', async () => {
    const user = userEvent.setup();
    render(<GracePeriodWidget />);
    await user.click(screen.getByRole('button', { name: 'Lizenz verlängern' }));
    expect(openRenewalMock).toHaveBeenCalledTimes(1);
  });
});
