/**
 * Global Jest setup for the Expo / React Native POS app (jest-expo preset).
 * Keep this file free of describe/test blocks — it is not a suite.
 */

jest.mock('@react-native-async-storage/async-storage', () =>
  require('@react-native-async-storage/async-storage/jest/async-storage-mock')
);

// Lightweight SVG / QR mocks for unit tests (native SVG is unavailable in Jest).
jest.mock('react-native-svg', () => {
  const React = require('react');
  const Mock = (props: { children?: React.ReactNode; testID?: string }) =>
    React.createElement('Svg', props, props.children);
  return {
    __esModule: true,
    default: Mock,
    Svg: Mock,
    Path: 'Path',
    Rect: 'Rect',
    Circle: 'Circle',
    G: 'G',
    Defs: 'Defs',
    ClipPath: 'ClipPath',
    LinearGradient: 'LinearGradient',
    Stop: 'Stop',
  };
});

jest.mock('react-native-qrcode-svg', () => {
  const React = require('react');
  const { View } = require('react-native');
  return {
    __esModule: true,
    default: ({ value, size, testID }: { value?: string; size?: number; testID?: string }) =>
      React.createElement(View, {
        testID: testID ?? 'mock-qrcode-svg',
        accessibilityLabel: value,
        style: { width: size, height: size },
      }),
  };
});

// Ensure React Native / Expo code paths see a development flag.
(globalThis as { __DEV__?: boolean }).__DEV__ = true;

// Quiet noisy logs during unit tests; keep errors visible.
const originalError = console.error;
beforeAll(() => {
  jest.spyOn(console, 'log').mockImplementation(() => {});
  jest.spyOn(console, 'warn').mockImplementation(() => {});
});

afterAll(() => {
  jest.restoreAllMocks();
});

beforeEach(() => {
  console.error = (...args: unknown[]) => {
    const first = args[0];
    if (
      typeof first === 'string' &&
      first.includes('Warning: ReactDOM.render is no longer supported')
    ) {
      return;
    }
    originalError.call(console, ...args);
  };
});

afterEach(() => {
  console.error = originalError;
  jest.clearAllMocks();
});
