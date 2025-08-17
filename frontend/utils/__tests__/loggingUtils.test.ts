// Global değişkenleri tanımla
(global as any).__DEV__ = true;

import { suppressDeprecatedWarnings, suppressAllDeprecatedWarnings, suppressSpecificDeprecatedWarnings } from '../loggingUtils';

// Mock LogBox
jest.mock('react-native', () => ({
  Platform: {
    OS: 'web'
  },
  LogBox: {
    ignoreLogs: jest.fn()
  }
}));

describe('LoggingUtils - Deprecated Warning Suppression', () => {
  let mockConsoleWarn: jest.SpyInstance;
  let mockConsoleError: jest.SpyInstance;
  let mockLogBoxIgnoreLogs: jest.Mock;

  beforeEach(() => {
    // Console mock'larını temizle
    mockConsoleWarn = jest.spyOn(console, 'warn').mockImplementation();
    mockConsoleError = jest.spyOn(console, 'error').mockImplementation();
    
    // LogBox mock'unu al
    const { LogBox } = require('react-native');
    mockLogBoxIgnoreLogs = LogBox.ignoreLogs as jest.Mock;
  });

  afterEach(() => {
    mockConsoleWarn.mockRestore();
    mockConsoleError.mockRestore();
    jest.clearAllMocks();
  });

  describe('suppressSpecificDeprecatedWarnings', () => {
    it('should call LogBox.ignoreLogs with specific deprecated warnings', () => {
      suppressSpecificDeprecatedWarnings();
      
      expect(mockLogBoxIgnoreLogs).toHaveBeenCalledWith([
        'shadow* style props are deprecated',
        'props.pointerEvents is deprecated',
        'style.resizeMode is deprecated'
      ]);
    });
  });

  describe('suppressDeprecatedWarnings', () => {
    it('should call LogBox.ignoreLogs and override console methods', () => {
      suppressDeprecatedWarnings();
      
      // LogBox.ignoreLogs çağrıldı mı kontrol et
      expect(mockLogBoxIgnoreLogs).toHaveBeenCalledWith([
        'shadow* style props are deprecated',
        'props.pointerEvents is deprecated',
        'style.resizeMode is deprecated',
        'Use "boxShadow"',
        'Use style.pointerEvents',
        'Use props.resizeMode'
      ]);
    });
  });

  describe('suppressAllDeprecatedWarnings', () => {
    it('should call LogBox.ignoreLogs with all deprecated warnings', () => {
      suppressAllDeprecatedWarnings();
      
      expect(mockLogBoxIgnoreLogs).toHaveBeenCalledWith([
        'shadow* style props are deprecated',
        'props.pointerEvents is deprecated',
        'style.resizeMode is deprecated',
        'Use "boxShadow"',
        'Use style.pointerEvents',
        'Use props.resizeMode',
        'deprecated',
        'shadow*',
        'pointerEvents',
        'resizeMode'
      ]);
    });
  });
});
