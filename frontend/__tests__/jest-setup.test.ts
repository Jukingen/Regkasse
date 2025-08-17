import '@testing-library/jest-dom';
import { jest } from '@jest/globals';

// Global test setup
beforeAll(() => {
  // Global değişkenleri tanımla
  (global as any).__DEV__ = true;
  
  // Console.log'ları test ortamında sustur
  jest.spyOn(console, 'log').mockImplementation(() => {});
  jest.spyOn(console, 'warn').mockImplementation(() => {});
  
  // Network isteklerini engelle
  (global as any).fetch = jest.fn();
  
  // Mock localStorage
  const localStorageMock = {
    getItem: jest.fn(),
    setItem: jest.fn(),
    removeItem: jest.fn(),
    clear: jest.fn(),
    length: 0,
    key: jest.fn(),
  };
  (global as any).localStorage = localStorageMock;
  
  // Mock sessionStorage
  const sessionStorageMock = {
    getItem: jest.fn(),
    setItem: jest.fn(),
    removeItem: jest.fn(),
    clear: jest.fn(),
    length: 0,
    key: jest.fn(),
  };
  (global as any).sessionStorage = sessionStorageMock;
  
  // Mock window.matchMedia
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: jest.fn().mockImplementation(query => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: jest.fn(),
      removeListener: jest.fn(),
      addEventListener: jest.fn(),
      removeEventListener: jest.fn(),
      dispatchEvent: jest.fn(),
    })),
  });
  
  // Mock IntersectionObserver
  (global as any).IntersectionObserver = jest.fn().mockImplementation(() => ({
    observe: jest.fn(),
    unobserve: jest.fn(),
    disconnect: jest.fn(),
  }));
  
  // Mock ResizeObserver
  (global as any).ResizeObserver = jest.fn().mockImplementation(() => ({
    observe: jest.fn(),
    unobserve: jest.fn(),
    disconnect: jest.fn(),
  }));
});

afterAll(() => {
  jest.restoreAllMocks();
});

// Test ortamında hata mesajlarını daha açık göster
const originalError = console.error;
beforeEach(() => {
  console.error = (...args: any[]) => {
    if (
      typeof args[0] === 'string' &&
      args[0].includes('Warning: ReactDOM.render is no longer supported')
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

// Jest setup test
describe('Jest Setup', () => {
  test('Jest is working correctly', () => {
    expect(true).toBe(true);
  });
});
