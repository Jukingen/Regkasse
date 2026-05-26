import {
  buildLicenseRenewalMailtoUrl,
  getLicenseExtensionHttpUrl,
  handleLicenseRenewal,
} from '../constants/licenseRenewal';
import { openLicenseExtension } from '../utils/openAdmin';
import { openMailtoUrl } from '../utils/openLink';

jest.mock('../utils/openAdmin', () => ({
  openLicenseExtension: jest.fn(),
}));

jest.mock('../utils/openLink', () => ({
  openMailtoUrl: jest.fn(),
}));

describe('licenseRenewal helpers', () => {
  const prevAdminBaseUrl = process.env.EXPO_PUBLIC_ADMIN_BASE_URL;

  beforeEach(() => {
    jest.clearAllMocks();
    process.env.EXPO_PUBLIC_ADMIN_BASE_URL = 'https://admin.example.com';
  });

  afterAll(() => {
    if (prevAdminBaseUrl == null) {
      delete process.env.EXPO_PUBLIC_ADMIN_BASE_URL;
    } else {
      process.env.EXPO_PUBLIC_ADMIN_BASE_URL = prevAdminBaseUrl;
    }
  });

  test('handleLicenseRenewal prefers admin redirect when machine hash is known', async () => {
    (openLicenseExtension as jest.Mock).mockResolvedValue(true);

    await expect(
      handleLicenseRenewal({
        machineHash: 'abc123',
        daysRemaining: 12,
        isTrial: false,
        isExpired: false,
      }),
    ).resolves.toBe(true);

    expect(openLicenseExtension).toHaveBeenCalledWith('abc123');
    expect(openMailtoUrl).not.toHaveBeenCalled();
  });

  test('handleLicenseRenewal falls back to support mail when machine hash is missing', async () => {
    (openMailtoUrl as jest.Mock).mockResolvedValue(true);

    await expect(
      handleLicenseRenewal({
        machineHash: null,
        daysRemaining: 3,
        isTrial: true,
        isExpired: false,
      }),
    ).resolves.toBe(true);

    expect(openMailtoUrl).toHaveBeenCalledWith(
      buildLicenseRenewalMailtoUrl({
        machineHash: null,
        daysRemaining: 3,
        isTrial: true,
        isExpired: false,
      }),
    );
  });

  test('deprecated URL helper still builds a working extension URL', () => {
    const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => undefined);

    expect(getLicenseExtensionHttpUrl('abc123')).toBe(
      'https://admin.example.com/admin/license?intent=extend&machineHash=abc123',
    );
    expect(warnSpy).toHaveBeenCalledWith(
      'getLicenseExtensionHttpUrl is deprecated, use openLicenseExtension',
    );

    warnSpy.mockRestore();
  });
});
