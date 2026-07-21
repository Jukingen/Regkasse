import {
  allowedOnPlatformHost,
  buildAdminUrl,
  getAdminBaseUrl,
  isAdminTargetAvailable,
  requiresTenant,
} from '../constants/adminRoutes';
import { adminRedirector } from '../src/features/admin-navigation/openAdmin';
import * as openLink from '../utils/openLink';

jest.mock('../utils/openLink', () => ({
  openHttpOrHttpsUrl: jest.fn(),
}));

jest.mock('@/services/tenant/tenantStorage', () => ({
  getCurrentTenantSlug: jest.fn().mockResolvedValue('dev'),
}));

jest.mock('react-native', () => ({
  Alert: {
    alert: jest.fn(),
  },
  Linking: {
    openURL: jest.fn().mockResolvedValue(undefined),
  },
  Platform: {
    OS: 'ios',
    select: (spec: Record<string, unknown>) => spec.ios ?? spec.default,
  },
}));

describe('adminRedirector', () => {
  const prevAdminBaseUrl = process.env.EXPO_PUBLIC_ADMIN_BASE_URL;
  const prevLicenseExtensionUrl = process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL;

  afterEach(() => {
    jest.restoreAllMocks();

    if (prevAdminBaseUrl == null) {
      delete process.env.EXPO_PUBLIC_ADMIN_BASE_URL;
    } else {
      process.env.EXPO_PUBLIC_ADMIN_BASE_URL = prevAdminBaseUrl;
    }

    if (prevLicenseExtensionUrl == null) {
      delete process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL;
    } else {
      process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL = prevLicenseExtensionUrl;
    }
  });

  test('builds license extension URL from admin base URL', () => {
    process.env.EXPO_PUBLIC_ADMIN_BASE_URL = 'https://admin.example.com/';
    delete process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL;

    expect(getAdminBaseUrl()).toBe('https://admin.example.com');
    expect(isAdminTargetAvailable('licenseExtend')).toBe(true);
    expect(requiresTenant('tenantUsers')).toBe(true);
    expect(allowedOnPlatformHost('tenantManagement')).toBe(true);
    expect(buildAdminUrl('licenseExtend', { machineHash: 'abc123' })).toBe(
      'https://admin.example.com/admin/license?intent=extend&machineHash=abc123'
    );
  });

  test('prefers explicit license extension URL when configured', () => {
    process.env.EXPO_PUBLIC_ADMIN_BASE_URL = 'https://admin.example.com';
    process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL =
      'https://customer.example.com/admin/license?source=pos';

    expect(buildAdminUrl('licenseExtend', { machineHash: 'abc123' })).toBe(
      'https://customer.example.com/admin/license?source=pos&intent=extend&machineHash=abc123'
    );
  });

  test('builds tenant route URLs and validates required context', () => {
    process.env.EXPO_PUBLIC_ADMIN_BASE_URL = 'https://admin.example.com';
    delete process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL;

    expect(buildAdminUrl('tenantUsers', { tenantId: 'tenant-123', returnTo: '/dashboard' })).toBe(
      'https://admin.example.com/admin/tenants/tenant-123/users?returnTo=%2Fdashboard'
    );
    expect(isAdminTargetAvailable('tenantUsers')).toBe(false);
    expect(() => buildAdminUrl('tenantUsers')).toThrow('Context required for tenantUsers');
  });

  test('falls back to local admin host in non-production environments', () => {
    delete process.env.EXPO_PUBLIC_ADMIN_BASE_URL;
    delete process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL;

    expect(getAdminBaseUrl()).toBe('http://admin.regkasse.local:3000');
    expect(buildAdminUrl('tenantManagement')).toBe(
      'http://admin.regkasse.local:3000/admin/tenants'
    );
  });

  test('delegates URL opening to openLink helper', async () => {
    process.env.EXPO_PUBLIC_ADMIN_BASE_URL = 'https://admin.example.com';
    delete process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL;

    const openSpy = jest.spyOn(openLink, 'openHttpOrHttpsUrl').mockResolvedValue(true);

    await expect(
      adminRedirector.openAdmin('licenseExtend', { machineHash: 'abc123' })
    ).resolves.toBe(true);

    expect(openSpy).toHaveBeenCalledWith(
      'https://admin.example.com/admin/license?intent=extend&machineHash=abc123',
      { forceWebBrowser: undefined }
    );
  });
});
