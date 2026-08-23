import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
import { TenantHeader } from '@/shared/components/TenantHeader';

const activeLicense: ResolvedLicenseStatus = {
  kind: 'active',
  daysRemaining: 30,
  daysExpired: 0,
  canWrite: true,
  canManageUsers: true,
  canAccess: true,
};

const tenant = {
  id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
  slug: 'dev',
  name: 'Development',
  licenseValid: true,
  licenseValidUntilUtc: '2026-12-31T00:00:00Z',
};

vi.mock('@/hooks/useTenant', () => ({
  useTenant: () => ({ tenant }),
}));

vi.mock('@/features/license/hooks/useLicense', () => ({
  useLicense: () => ({ licenseStatus: { ...activeLicense, isReadOnly: false } }),
}));

vi.mock('@/features/tenancy/hooks/useSuperAdminTenantMode', () => ({
  useSuperAdminTenantMode: () => ({ requiresTenantSelection: false }),
}));

vi.mock('@/features/tenancy/hooks/useTenantContext', () => ({
  useTenantContext: () => ({
    jwtTenantSlug: 'dev',
    isDevTenantOverride: false,
    isImpersonating: false,
    isPlatformAdminHost: false,
  }),
}));

vi.mock('@/features/tenant/hooks/useTenantInfo', () => ({
  useTenantInfo: () => ({
    tenantSlug: 'dev',
    tenantId: tenant.id,
    tenantName: 'Development',
    registeredAt: '2026-01-15T00:00:00Z',
    licenseStatus: activeLicense,
    hasAuthToken: true,
    isLoading: false,
  }),
}));

vi.mock('@/i18n', () => ({
  formatDate: () => '15.01.2026',
  useI18n: () => ({
    formatLocale: 'de-DE',
    t: (key: string) => {
      const labels: Record<string, string> = {
        'common.tenant.tenant': 'Mandant',
        'common.tenant.tenantAlt': 'Firma',
        'adminShell.tenant.infoCardTitle': 'Aktuelle Firma',
        'adminShell.tenant.infoCardId': 'Firmen-ID',
        'adminShell.tenant.infoCardJwtSlug': 'Mandant im JWT',
        'adminShell.tenant.info.license': 'Lizenzstatus',
        'adminShell.tenant.info.registeredAt': 'Registriert am',
        'license.phase.labels.active': 'Aktiv',
        'license.phase.messages.tenant.active': 'Lizenz aktiv',
        'license.phase.daysRemaining': 'Noch 30 Tage',
      };
      return labels[key] ?? key;
    },
  }),
}));

describe('TenantHeader', () => {
  it('renders compact mandant name, slug, truncated id, license, and registration date', () => {
    const { container } = render(<TenantHeader />);

    expect(container.querySelector('.tenant-header')).toBeTruthy();
    expect(screen.getByText('Development')).toBeTruthy();
    expect(screen.getByText('(dev)')).toBeTruthy();
    expect(screen.getByText('aaaaaaaa…')).toBeTruthy();
    expect(screen.getByText('Aktiv')).toBeTruthy();
    expect(screen.getByText('15.01.2026')).toBeTruthy();
    expect(container.querySelector('.ant-card')).toBeNull();
  });
});
