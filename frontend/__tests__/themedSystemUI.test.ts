/**
 * @jest-environment node
 */
import { beforeEach, describe, expect, jest, test } from '@jest/globals';

import * as platformUtils from '../utils/platformUtils';
import { ANDROID_NAVIGATION_BAR_HIDDEN, applySystemUiForTheme } from '../utils/systemUi';

const mockSetBackgroundColorAsync = jest.fn(async (..._args: unknown[]) => undefined);
const mockGetBackgroundColorAsync = jest.fn(async (..._args: unknown[]) => null);
const mockSetVisibilityAsync = jest.fn(async (..._args: unknown[]) => undefined);
const mockSetStyle = jest.fn((..._args: unknown[]) => undefined);
const mockSetHidden = jest.fn((..._args: unknown[]) => undefined);

jest.mock('expo-system-ui', () => ({
  setBackgroundColorAsync: (...args: unknown[]) => mockSetBackgroundColorAsync(...args),
  getBackgroundColorAsync: (...args: unknown[]) => mockGetBackgroundColorAsync(...args),
}));

jest.mock('expo-navigation-bar', () => {
  const setStyle = (...args: unknown[]) => {
    mockSetStyle(...args);
  };
  const setHidden = (...args: unknown[]) => {
    mockSetHidden(...args);
  };
  const NavigationBar = {
    setStyle,
    setHidden,
  };
  return {
    NavigationBar,
    setVisibilityAsync: (...args: unknown[]) => mockSetVisibilityAsync(...args),
    setStyle,
    setHidden,
  };
});

jest.mock('../utils/platformUtils', () => {
  const state = { native: true, android: false, ios: true, web: false };
  return {
    get isNative() {
      return state.native;
    },
    get isAndroid() {
      return state.android;
    },
    get isIOS() {
      return state.ios;
    },
    get isWeb() {
      return state.web;
    },
    __setPlatform(next: Partial<typeof state>) {
      Object.assign(state, next);
    },
  };
});

function setPlatform(flags: { native?: boolean; android?: boolean; ios?: boolean; web?: boolean }) {
  (
    platformUtils as unknown as {
      __setPlatform: (next: Record<string, boolean>) => void;
    }
  ).__setPlatform({
    native: true,
    android: false,
    ios: true,
    web: false,
    ...flags,
  });
}

describe('applySystemUiForTheme', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockSetBackgroundColorAsync.mockImplementation(async () => undefined);
    mockSetVisibilityAsync.mockImplementation(async () => undefined);
    setPlatform({ native: true, android: false, ios: true, web: false });
  });

  test('POS keeps Android navigation bar visible by default', () => {
    expect(ANDROID_NAVIGATION_BAR_HIDDEN).toBe(false);
  });

  test('sets SystemUI root background on native iOS (no nav-bar APIs)', async () => {
    await applySystemUiForTheme('#F5F5F5', false);

    expect(mockSetBackgroundColorAsync).toHaveBeenCalledWith('#F5F5F5');
    expect(mockSetVisibilityAsync).not.toHaveBeenCalled();
    expect(mockSetStyle).not.toHaveBeenCalled();
  });

  test('on Android syncs nav bar visibility + button style for light theme', async () => {
    setPlatform({ native: true, android: true, ios: false, web: false });

    await applySystemUiForTheme('#F5F5F5', false);

    expect(mockSetBackgroundColorAsync).toHaveBeenCalledWith('#F5F5F5');
    expect(mockSetHidden).toHaveBeenCalledWith(false);
    expect(mockSetStyle).toHaveBeenCalledWith('dark');
  });

  test('on Android uses light nav-button style for dark theme', async () => {
    setPlatform({ native: true, android: true, ios: false, web: false });

    await applySystemUiForTheme('#000000', true);

    expect(mockSetBackgroundColorAsync).toHaveBeenCalledWith('#000000');
    expect(mockSetHidden).toHaveBeenCalledWith(false);
    expect(mockSetStyle).toHaveBeenCalledWith('light');
  });

  test('no-ops on web', async () => {
    setPlatform({ native: false, android: false, ios: false, web: true });

    await applySystemUiForTheme('#F5F5F5', false);

    expect(mockSetBackgroundColorAsync).not.toHaveBeenCalled();
  });

  test('ignores SystemUI rejection without throwing', async () => {
    mockSetBackgroundColorAsync.mockImplementation(async () => {
      throw new Error('unsupported');
    });

    await expect(applySystemUiForTheme('#F5F5F5', false)).resolves.toBeUndefined();
  });
});
