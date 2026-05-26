import { openAdmin, openLicenseExtension } from '../utils/openAdmin';
import * as openLink from '../utils/openLink';

import { getCurrentTenantSlug } from '@/services/tenant/tenantStorage';

jest.mock('../utils/openLink', () => ({
  openHttpOrHttpsUrl: jest.fn(),
}));

jest.mock('@/services/tenant/tenantStorage', () => ({
  getCurrentTenantSlug: jest.fn(),
}));

const alertMock = jest.fn();
const openUrlMock = jest.fn();

jest.mock('react-native', () => ({
  Alert: {
    alert: (...args: unknown[]) => alertMock(...args),
  },
  Linking: {
    openURL: (...args: unknown[]) => openUrlMock(...args),
  },
}));

describe('openAdmin', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (getCurrentTenantSlug as jest.Mock).mockResolvedValue('cafe');
    (openLink.openHttpOrHttpsUrl as jest.Mock).mockResolvedValue(true);
    openUrlMock.mockResolvedValue(undefined);
  });

  test('opens admin target when tenant context is available', async () => {
    await expect(
      openAdmin('licenseExtend', { machineHash: 'abc123', intent: 'extend' }),
    ).resolves.toBe(true);

    expect(openLink.openHttpOrHttpsUrl).toHaveBeenCalledWith(
      'http://admin.regkasse.local:3000/admin/license?intent=extend&machineHash=abc123',
      { forceWebBrowser: undefined },
    );
  });

  test('blocks platform-only restricted target without tenant', async () => {
    (getCurrentTenantSlug as jest.Mock).mockResolvedValue(null);
    alertMock.mockImplementation(
      (_title: string, _message: string, buttons?: { onPress?: () => void }[]) => {
        buttons?.[0]?.onPress?.();
      },
    );

    const result = await openAdmin('cashRegisters');

    expect(result).toBe(false);
    expect(alertMock).toHaveBeenCalledWith(
      'Mandant erforderlich',
      'Diese Aktion erfordert einen ausgewählten Mandanten. Möchten Sie trotzdem fortfahren?',
      expect.any(Array),
    );
  });

  test('opens support mail when browser launch fails and fallback is enabled', async () => {
    (openLink.openHttpOrHttpsUrl as jest.Mock).mockResolvedValue(false);

    await expect(
      openLicenseExtension('abc123'),
    ).resolves.toBe(true);

    expect(openUrlMock).toHaveBeenCalledWith(
      'mailto:support@regkasse.at?subject=Lizenzverl%C3%A4ngerung&body=Bitte%20verl%C3%A4ngern%20Sie%20meine%20Lizenz.%0A%0AMaschinen-Fingerprint%3A%20abc123%0A%0AVielen%20Dank.',
    );
  });
});
