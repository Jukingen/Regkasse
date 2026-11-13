import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { LicenseStatusIndicator } from '@/components/admin-layout/LicenseStatusIndicator';
import type { LicenseStatus } from '@/features/license/hooks/useLicenseStatus';

import {
  ACTIVE_UNTIL,
  EXTENDED_UNTIL,
  interpolateT,
  resolvedLicense,
  tenantLicenseStatus,
} from '@/features/license/components/__tests__/licenseUiTestFixtures';

type HeaderLicenseMock = {
  mode: 'hidden' | 'tenant';
  resolvedStatus: LicenseStatus | null;
  licenseValidUntilUtc: string | null;
  isLoading: boolean;
  isUnavailable: boolean;
};

const headerLicense = vi.hoisted(() => ({
  current: {
    mode: 'tenant' as const,
    resolvedStatus: null as LicenseStatus | null,
    licenseValidUntilUtc: null as string | null,
    isLoading: false,
    isUnavailable: false,
  },
}));

vi.mock('@/features/tenant/hooks/useHeaderTenantLicense', () => ({
  useHeaderTenantLicense: () => headerLicense.current,
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: interpolateT({
      'license.badge.loading': 'Lizenzstatus…',
      'license.badge.unavailable': 'Lizenz unbekannt',
      'license.badge.unavailableTooltip':
        'Lizenzstatus konnte nicht geladen werden (fehlende Berechtigung oder Serverfehler).',
      'license.badge.headerShort.none': 'Keine Mandantenlizenz',
      'license.badge.headerShort.expired': 'Lizenz abgelaufen',
      'license.badge.headerShort.expiringSoon': 'Lizenz läuft bald ab',
      'license.badge.headerShort.expiringSoonWithDays': 'Läuft ab in {{days}} Tagen',
      'license.badge.headerShort.expiringSoonWithHours': 'Läuft ab in {{hours}} Std.',
      'license.badge.headerShort.daysRemaining': '{{days}} Tage',
      'license.badge.headerShort.licensed': 'Lizenziert',
      'license.badge.headerShort.mandantTooltip': 'Mandantenlizenz: {{status}}',
      'license.badge.headerShort.tooltip.validUntil': 'Gültig bis',
      'license.badge.headerShort.tooltip.daysRemaining': 'Verbleibende Tage',
      'license.badge.headerShort.tooltip.hoursRemaining': 'Verbleibende Stunden',
      'license.badge.headerShort.tooltip.status': 'Status',
      'license.badge.headerShort.tooltip.countdown': 'Läuft ab in: {{countdown}}',
      'license.badge.headerShort.tooltip.ariaSummary':
        'Gültig bis: {{dateTime}}. Verbleibende Tage: {{days}}. Status: {{status}}.',
      'license.phase.labels.active': 'Aktiv',
      'license.phase.labels.expired': 'Abgelaufen',
      'license.phase.labels.graceWrite': 'Grace-Phase: Schreiben erlaubt',
    }),
  }),
}));

function tenantHeader(partial: Partial<HeaderLicenseMock>): HeaderLicenseMock {
  return {
    mode: 'tenant',
    resolvedStatus: null,
    licenseValidUntilUtc: null,
    isLoading: false,
    isUnavailable: false,
    ...partial,
  };
}

describe('LicenseStatusIndicator', () => {
  beforeEach(() => {
    headerLicense.current = tenantHeader({ resolvedStatus: null });
  });

  it('shows Aktiv and remaining days for an active license', () => {
    headerLicense.current = tenantHeader({
      resolvedStatus: tenantLicenseStatus('active'),
      licenseValidUntilUtc: ACTIVE_UNTIL,
    });
    render(<LicenseStatusIndicator />);

    expect(screen.getByText('Aktiv (45 Tage)')).toBeInTheDocument();
    expect(screen.getByText('Aktiv (45 Tage)').closest('.license-badge')).toHaveClass('valid');
  });

  it('shows expired status when the license is locked', () => {
    headerLicense.current = tenantHeader({
      resolvedStatus: tenantLicenseStatus('lockdown'),
      licenseValidUntilUtc: '2026-01-01T00:00:00.000Z',
    });
    render(<LicenseStatusIndicator />);

    expect(screen.getByText('Lizenz abgelaufen')).toBeInTheDocument();
    expect(screen.getByText('Lizenz abgelaufen').closest('.license-badge')).toHaveClass('expired');
  });

  it('shows an expiring-soon warning during grace', () => {
    headerLicense.current = tenantHeader({
      resolvedStatus: tenantLicenseStatus('grace_write'),
      licenseValidUntilUtc: '2026-01-01T00:00:00.000Z',
    });
    render(<LicenseStatusIndicator />);

    expect(screen.getByText('Lizenz läuft bald ab')).toBeInTheDocument();
    expect(screen.getByText('Lizenz läuft bald ab').closest('.license-badge')).toHaveClass(
      'warning'
    );
  });

  it('after extension shows Aktiv with the updated remaining days', () => {
    headerLicense.current = tenantHeader({
      resolvedStatus: tenantLicenseStatus('lockdown'),
      licenseValidUntilUtc: '2026-01-01T00:00:00.000Z',
    });
    const { rerender } = render(<LicenseStatusIndicator />);
    expect(screen.getByText('Lizenz abgelaufen')).toBeInTheDocument();

    headerLicense.current = tenantHeader({
      resolvedStatus: tenantLicenseStatus('active', {
        ...resolvedLicense('active', { daysRemaining: 504 }),
        daysRemaining: 504,
        daysExpired: 0,
        isExpired: false,
        isLocked: false,
      }),
      licenseValidUntilUtc: EXTENDED_UNTIL,
    });
    rerender(<LicenseStatusIndicator />);

    expect(screen.getByText('Aktiv (504 Tage)')).toBeInTheDocument();
    expect(screen.queryByText('Lizenz abgelaufen')).not.toBeInTheDocument();
  });
});
