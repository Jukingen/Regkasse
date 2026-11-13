import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import type { NetInfoState } from '@react-native-community/netinfo';

import { fetchIsNetworkOnline, isNetworkOnline } from '@/utils/isNetworkOnline';

const mockFetch =
  jest.fn<() => Promise<Pick<NetInfoState, 'isConnected' | 'isInternetReachable'>>>();
const mockAddEventListener = jest.fn(() => jest.fn());

jest.mock('@react-native-community/netinfo', () => ({
  __esModule: true,
  default: {
    fetch: (...args: unknown[]) => mockFetch(...(args as [])),
    addEventListener: (...args: unknown[]) => mockAddEventListener(...(args as [])),
  },
}));

const mockIsDevSimulatePosNetworkOffline = jest.fn(() => false);

jest.mock('@/constants/devSimulatePosOffline', () => ({
  isDevSimulatePosNetworkOffline: () => mockIsDevSimulatePosNetworkOffline(),
}));

function state(
  partial: Pick<NetInfoState, 'isConnected' | 'isInternetReachable'>
): Pick<NetInfoState, 'isConnected' | 'isInternetReachable'> {
  return partial;
}

describe('isNetworkOnline', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockIsDevSimulatePosNetworkOffline.mockReturnValue(false);
  });

  it('is online when connected and reachable', () => {
    expect(isNetworkOnline(state({ isConnected: true, isInternetReachable: true }))).toBe(true);
  });

  it('treats null isInternetReachable as online when connected (reconnect edge case)', () => {
    expect(isNetworkOnline(state({ isConnected: true, isInternetReachable: null }))).toBe(true);
  });

  it('is offline when connected but explicitly unreachable', () => {
    expect(isNetworkOnline(state({ isConnected: true, isInternetReachable: false }))).toBe(false);
  });

  it('is offline when disconnected', () => {
    expect(isNetworkOnline(state({ isConnected: false, isInternetReachable: true }))).toBe(false);
  });

  it('is offline when isConnected is null/unknown', () => {
    expect(isNetworkOnline(state({ isConnected: null, isInternetReachable: true }))).toBe(false);
  });

  it('respects dev simulate-offline flag', () => {
    mockIsDevSimulatePosNetworkOffline.mockReturnValue(true);
    expect(isNetworkOnline(state({ isConnected: true, isInternetReachable: true }))).toBe(false);
  });
});

describe('fetchIsNetworkOnline', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockIsDevSimulatePosNetworkOffline.mockReturnValue(false);
  });

  it('returns NetInfo result via isNetworkOnline', async () => {
    mockFetch.mockResolvedValueOnce({
      isConnected: true,
      isInternetReachable: null,
    });
    await expect(fetchIsNetworkOnline()).resolves.toBe(true);
  });

  it('falls back to navigator.onLine when NetInfo.fetch throws', async () => {
    mockFetch.mockRejectedValueOnce(new Error('native module missing'));
    const original = global.navigator;
    Object.defineProperty(global, 'navigator', {
      configurable: true,
      value: { onLine: false },
    });
    try {
      await expect(fetchIsNetworkOnline()).resolves.toBe(false);
    } finally {
      Object.defineProperty(global, 'navigator', {
        configurable: true,
        value: original,
      });
    }
  });
});
