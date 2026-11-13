/**
 * @jest-environment node
 */
import { Platform } from 'react-native';

import { openHttpOrHttpsUrl, openMailtoUrl } from '../utils/openLink';

const mockOpenBrowserAsync = jest.fn();
const mockDismissBrowser = jest.fn();
const mockWarmUpAsync = jest.fn();
const mockMayInitWithUrlAsync = jest.fn();
const mockCoolDownAsync = jest.fn();
const mockOpenURL = jest.fn();
const mockAddEventListener = jest.fn();

jest.mock('expo-web-browser', () => ({
  openBrowserAsync: (...args: unknown[]) => mockOpenBrowserAsync(...args),
  dismissBrowser: (...args: unknown[]) => mockDismissBrowser(...args),
  warmUpAsync: (...args: unknown[]) => mockWarmUpAsync(...args),
  mayInitWithUrlAsync: (...args: unknown[]) => mockMayInitWithUrlAsync(...args),
  coolDownAsync: (...args: unknown[]) => mockCoolDownAsync(...args),
}));

jest.mock('expo-linking', () => ({
  openURL: (...args: unknown[]) => mockOpenURL(...args),
  addEventListener: (...args: unknown[]) => mockAddEventListener(...args),
}));

jest.mock('react-native', () => {
  const osRef = { current: 'ios' as string };
  return {
    Platform: {
      get OS() {
        return osRef.current;
      },
      setOS(next: string) {
        osRef.current = next;
      },
      select: (spec: Record<string, unknown>) => spec[osRef.current] ?? spec.default,
    },
  };
});

jest.mock('../utils/platformUtils', () => ({
  get isWeb() {
    const { Platform } = require('react-native') as { Platform: { OS: string } };
    return Platform.OS === 'web';
  },
  get isAndroid() {
    const { Platform } = require('react-native') as { Platform: { OS: string } };
    return Platform.OS === 'android';
  },
  get isIOS() {
    const { Platform } = require('react-native') as { Platform: { OS: string } };
    return Platform.OS === 'ios';
  },
  get isNative() {
    const { Platform } = require('react-native') as { Platform: { OS: string } };
    return Platform.OS === 'ios' || Platform.OS === 'android';
  },
  safeWindow: () => null,
}));

function setPlatform(os: 'ios' | 'android' | 'web') {
  (Platform as unknown as { setOS: (next: string) => void }).setOS(os);
}

describe('openHttpOrHttpsUrl', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    setPlatform('ios');
    mockOpenBrowserAsync.mockResolvedValue({ type: 'cancel' });
    mockDismissBrowser.mockResolvedValue({ type: 'dismiss' });
    mockWarmUpAsync.mockResolvedValue({});
    mockMayInitWithUrlAsync.mockResolvedValue({});
    mockCoolDownAsync.mockResolvedValue({});
    mockOpenURL.mockResolvedValue(undefined);
    mockAddEventListener.mockReturnValue({ remove: jest.fn() });
  });

  test('rejects non-http(s) URLs', async () => {
    await expect(openHttpOrHttpsUrl('cashregister://home')).resolves.toBe(false);
    expect(mockOpenBrowserAsync).not.toHaveBeenCalled();
  });

  test('opens via WebBrowser on iOS by default and registers deep-link dismiss', async () => {
    const remove = jest.fn();
    mockAddEventListener.mockReturnValue({ remove });

    await expect(openHttpOrHttpsUrl('https://admin.regkasse.at/admin')).resolves.toBe(true);

    expect(mockAddEventListener).toHaveBeenCalledWith('url', expect.any(Function));
    expect(mockOpenBrowserAsync).toHaveBeenCalledWith('https://admin.regkasse.at/admin', {
      enableDefaultShareMenuItem: true,
      showTitle: true,
      createTask: true,
      useProxyActivity: true,
    });
    expect(remove).toHaveBeenCalled();
    expect(mockWarmUpAsync).not.toHaveBeenCalled();
  });

  test('dismisses browser when deep-link url event fires on iOS', async () => {
    let urlHandler: (() => void) | undefined;
    mockAddEventListener.mockImplementation((_event: string, handler: () => void) => {
      urlHandler = handler;
      return { remove: jest.fn() };
    });
    mockOpenBrowserAsync.mockImplementation(async () => {
      urlHandler?.();
      return { type: 'dismiss' };
    });

    await openHttpOrHttpsUrl('https://example.com');

    expect(mockDismissBrowser).toHaveBeenCalled();
  });

  test('warms Custom Tabs on Android then cools down', async () => {
    setPlatform('android');
    mockOpenBrowserAsync.mockResolvedValue({ type: 'opened' });

    await expect(openHttpOrHttpsUrl('https://admin.regkasse.at')).resolves.toBe(true);

    expect(mockWarmUpAsync).toHaveBeenCalled();
    expect(mockMayInitWithUrlAsync).toHaveBeenCalledWith('https://admin.regkasse.at');
    expect(mockAddEventListener).not.toHaveBeenCalled();
    expect(mockCoolDownAsync).toHaveBeenCalled();
  });

  test('forceWebBrowser=false uses system Linking instead of WebBrowser', async () => {
    await expect(
      openHttpOrHttpsUrl('https://admin.regkasse.at', { forceWebBrowser: false })
    ).resolves.toBe(true);

    expect(mockOpenBrowserAsync).not.toHaveBeenCalled();
    expect(mockOpenURL).toHaveBeenCalledWith('https://admin.regkasse.at');
  });

  test('falls back to Linking when WebBrowser throws', async () => {
    mockOpenBrowserAsync.mockRejectedValue(new Error('no browser'));

    await expect(openHttpOrHttpsUrl('https://example.com')).resolves.toBe(true);

    expect(mockOpenURL).toHaveBeenCalledWith('https://example.com');
  });
});

describe('openMailtoUrl', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    setPlatform('ios');
    mockOpenURL.mockResolvedValue(undefined);
  });

  test('rejects non-mailto URLs', async () => {
    await expect(openMailtoUrl('https://example.com')).resolves.toBe(false);
  });

  test('opens mailto via Linking without canOpenURL gate', async () => {
    await expect(openMailtoUrl('mailto:support@regkasse.at')).resolves.toBe(true);
    expect(mockOpenURL).toHaveBeenCalledWith('mailto:support@regkasse.at');
  });
});
