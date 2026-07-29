import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  getConfiguredLicensePaymentUrl,
  redirectToLicensePayment,
  resolveLicensePaymentRedirectTarget,
} from '@/features/license/utils/licensePaymentRedirect';

describe('licensePaymentRedirect', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('reads NEXT_PUBLIC_LICENSE_PAYMENT_URL when set', () => {
    vi.stubEnv('NEXT_PUBLIC_LICENSE_PAYMENT_URL', 'https://pay.example/checkout');
    expect(getConfiguredLicensePaymentUrl()).toBe('https://pay.example/checkout');
    expect(resolveLicensePaymentRedirectTarget({ isSuperAdmin: false })).toEqual({
      href: 'https://pay.example/checkout',
      kind: 'external',
    });
  });

  it('falls back to billing for Super Admin and mailto for Manager', () => {
    vi.stubEnv('NEXT_PUBLIC_LICENSE_PAYMENT_URL', '');
    expect(resolveLicensePaymentRedirectTarget({ isSuperAdmin: true })).toEqual({
      href: '/admin/billing',
      kind: 'internal',
    });
    const manager = resolveLicensePaymentRedirectTarget({ isSuperAdmin: false });
    expect(manager.kind).toBe('mailto');
    expect(manager.href).toContain('mailto:support@regkasse.at');
  });

  it('redirectToLicensePayment uses pushInternal for FA routes', () => {
    vi.stubEnv('NEXT_PUBLIC_LICENSE_PAYMENT_URL', '');
    const pushInternal = vi.fn();
    redirectToLicensePayment({ isSuperAdmin: true, pushInternal });
    expect(pushInternal).toHaveBeenCalledWith('/admin/billing');
  });
});
