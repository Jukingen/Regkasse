import { describe, expect, it, vi } from 'vitest';

import {
  buildInviteTenantPickerLabel,
  buildTenantPortalHost,
  formatInviteTenantLicenseShort,
} from '@/features/users/utils/inviteTenantDisplay';

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  buildTenantPortalUrl: (slug: string) => `https://${slug}.regkasse.at`,
}));

vi.mock('@/features/super-admin/utils/tenantLicenseLabel', () => ({
  resolveTenantLicenseLabel: (until: string | null | undefined, key: string | null | undefined) => {
    if (!until && !key) return { kind: 'none', daysRemaining: null };
    if (until === 'expired') return { kind: 'expired', daysRemaining: -1 };
    if (key === 'TRIAL') return { kind: 'trial', daysRemaining: 20 };
    if (until === 'soon') return { kind: 'valid', daysRemaining: 10 };
    return { kind: 'valid', daysRemaining: 120 };
  },
}));

vi.mock('@/features/super-admin/utils/tenantSelectorLabel', () => ({
  getTenantSelectorStatus: (tenant: { ownerAdminEmail?: string | null; isDemoPreset?: boolean }) => {
    if (tenant.isDemoPreset) return { kind: 'demo' };
    if (!tenant.ownerAdminEmail) return { kind: 'noAdmin' };
    return { kind: 'ok' };
  },
}));

const t = (key: string, params?: Record<string, string | number>) =>
  params ? `${key}:${JSON.stringify(params)}` : key;

describe('inviteTenantDisplay', () => {
  it('builds portal host without protocol', () => {
    expect(buildTenantPortalHost('cafe')).toBe('cafe.regkasse.at');
  });

  it('formats short license labels for picker', () => {
    expect(formatInviteTenantLicenseShort({ licenseValidUntilUtc: null, licenseKey: null }, t)).toBe(
      'users.create.licenseNone'
    );
    expect(
      formatInviteTenantLicenseShort({ licenseValidUntilUtc: 'expired', licenseKey: 'X' }, t)
    ).toBe('license.badge.expired.label');
    expect(
      formatInviteTenantLicenseShort({ licenseValidUntilUtc: 'x', licenseKey: 'TRIAL' }, t)
    ).toContain('license.badge.trial.label');
    expect(
      formatInviteTenantLicenseShort({ licenseValidUntilUtc: 'soon', licenseKey: 'REGK' }, t)
    ).toContain('license.badge.trial.label');
    expect(
      formatInviteTenantLicenseShort({ licenseValidUntilUtc: 'far', licenseKey: 'REGK' }, t)
    ).toBe('license.badge.licensed.label');
  });

  it('builds picker label with optional status suffixes', () => {
    const label = buildInviteTenantPickerLabel(
      {
        name: 'Cafe',
        slug: 'cafe',
        licenseValidUntilUtc: 'far',
        licenseKey: 'REGK',
        ownerAdminEmail: null,
        isDemoPreset: false,
      } as never,
      t
    );
    expect(label).toContain('users.create.tenantOption');
    expect(label).toContain('superadmin.selector.noAdmin');
  });
});
